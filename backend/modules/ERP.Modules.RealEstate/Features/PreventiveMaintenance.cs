using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.RealEstate.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.RealEstate.Features;

public sealed record PmScheduleInput(Guid PropertyId, Guid? UnitId, string Title, int FrequencyMonths, DateOnly NextDue, Priority Priority = Priority.Normal, string? Category = null,
    int LeadDays = 7, decimal EstimatedCost = 0, bool IsActive = true, string? Notes = null);

public sealed record PmScheduleDto(Guid Id, Guid PropertyId, Guid? UnitId, string Title, string? Category, Priority Priority, int FrequencyMonths, DateOnly NextDue, int LeadDays, decimal EstimatedCost, bool IsActive, DateOnly? LastGeneratedOn, string? Notes)
{
    public static PmScheduleDto From(PmSchedule s) => new(s.Id, s.PropertyId, s.UnitId, s.Title, s.Category, s.Priority, s.FrequencyMonths, s.NextDue, s.LeadDays, s.EstimatedCost, s.IsActive, s.LastGeneratedOn, s.Notes);
}

public sealed record PmRunResultDto(int WorkOrdersCreated);

public sealed record CreatePmSchedule(PmScheduleInput Input) : ICommand<PmScheduleDto>, IRequirePermission { public string RequiredPermission => P.PmManage; }
public sealed record UpdatePmSchedule(Guid Id, PmScheduleInput Input) : ICommand<PmScheduleDto>, IRequirePermission { public string RequiredPermission => P.PmManage; }
/// <summary>Raises the work orders that became due (the nightly job does the same).</summary>
public sealed record RunPreventiveMaintenance : ICommand<PmRunResultDto>, IRequirePermission { public string RequiredPermission => P.PmManage; }

public sealed class PmScheduleInputValidator : AbstractValidator<PmScheduleInput>
{
    public PmScheduleInputValidator()
    {
        RuleFor(x => x.PropertyId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.FrequencyMonths).InclusiveBetween(1, 120);
        RuleFor(x => x.LeadDays).InclusiveBetween(0, 365);
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.EstimatedCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Category).MaximumLength(50);
    }
}
public sealed class CreatePmScheduleValidator : AbstractValidator<CreatePmSchedule> { public CreatePmScheduleValidator() => RuleFor(x => x.Input).SetValidator(new PmScheduleInputValidator()); }
public sealed class UpdatePmScheduleValidator : AbstractValidator<UpdatePmSchedule> { public UpdatePmScheduleValidator() => RuleFor(x => x.Input).SetValidator(new PmScheduleInputValidator()); }

/// <summary>Raises a work order for every active schedule whose lead time has arrived, then moves it to its next occurrence. Idempotent per day.</summary>
internal sealed class PmGenerator(IRepository<PmSchedule> schedules, IRepository<WorkOrder> workOrders, INumberingService numbers, IUnitOfWork unitOfWork)
{
    public Task<int> RunAsync(DateOnly today, CancellationToken ct) => unitOfWork.InTransactionAsync(async token =>
    {
        var due = await schedules.Tracked(new Specification<PmSchedule>(s => s.IsActive)).ToListAsync(token);
        var created = 0;
        foreach (var s in due.Where(s => s.NextDue.AddDays(-s.LeadDays) <= today))
        {
            var w = new WorkOrder
            {
                LegalEntityId = s.LegalEntityId, PropertyId = s.PropertyId, UnitId = s.UnitId, Issue = s.Title, Category = s.Category, Priority = s.Priority,
                ReportedOn = today, ReportedBy = "Preventive maintenance", ScheduledFor = s.NextDue, EstimatedCost = s.EstimatedCost, Details = s.Notes,
            };
            w.Number = await numbers.NextAsync(Docs.WorkOrder, w.LegalEntityId, today, token);
            workOrders.Add(w);
            created++;
            s.LastGeneratedOn = today;
            var next = s.NextDue.AddMonths(s.FrequencyMonths);
            while (next.AddDays(-s.LeadDays) <= today) next = next.AddMonths(s.FrequencyMonths); // skip occurrences already missed
            s.NextDue = next;
        }
        await schedules.SaveChangesAsync(token);
        return created;
    }, ct);
}

internal sealed class PmJob(PmGenerator generator) : IScheduledJob
{
    public string Name => "realestate.preventive-maintenance";
    public string? ModuleKey => RealEstateModule.Key;
    public Task RunAsync(DateOnly today, CancellationToken ct) => generator.RunAsync(today, ct);
}

internal sealed class PmHandlers(IRepository<PmSchedule> schedules, IReadRepository<Property> properties, IReadRepository<Domain.Unit> units, PmGenerator generator, IClock clock) :
    IRequestHandler<CreatePmSchedule, PmScheduleDto>,
    IRequestHandler<UpdatePmSchedule, PmScheduleDto>,
    IRequestHandler<RunPreventiveMaintenance, PmRunResultDto>
{
    public async Task<PmScheduleDto> Handle(CreatePmSchedule cmd, CancellationToken ct)
    {
        var s = new PmSchedule();
        await MapAsync(cmd.Input, s, ct);
        schedules.Add(s);
        await schedules.SaveChangesAsync(ct);
        return PmScheduleDto.From(s);
    }

    public async Task<PmScheduleDto> Handle(UpdatePmSchedule cmd, CancellationToken ct)
    {
        var s = await schedules.GetForUpdateAsync(cmd.Id, ct);
        if (cmd.Input.PropertyId != s.PropertyId) throw new FieldValidationException("propertyId", "A schedule cannot move to another property.");
        await MapAsync(cmd.Input, s, ct);
        await schedules.SaveChangesAsync(ct);
        return PmScheduleDto.From(s);
    }

    public async Task<PmRunResultDto> Handle(RunPreventiveMaintenance cmd, CancellationToken ct)
        => new(await generator.RunAsync(DateOnly.FromDateTime(clock.UtcNow), ct));

    private async Task MapAsync(PmScheduleInput i, PmSchedule s, CancellationToken ct)
    {
        var property = await properties.FindAsync(i.PropertyId, ct) ?? throw new FieldValidationException("propertyId", "Property not found.");
        if (i.UnitId is { } unitId && !await units.AnyAsync(u => u.Id == unitId && u.PropertyId == i.PropertyId, ct)) throw new FieldValidationException("unitId", "That unit is not in this property.");
        s.LegalEntityId = property.LegalEntityId;
        s.PropertyId = i.PropertyId; s.UnitId = i.UnitId; s.Title = i.Title.Trim(); s.Category = i.Category; s.Priority = i.Priority; s.FrequencyMonths = i.FrequencyMonths;
        s.NextDue = i.NextDue; s.LeadDays = i.LeadDays; s.EstimatedCost = i.EstimatedCost; s.IsActive = i.IsActive; s.Notes = i.Notes;
    }
}
