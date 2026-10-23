using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.RealEstate.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

public sealed record InspectionItemInput(string Area, ItemCondition Condition, string? Notes = null, decimal RepairCost = 0);
public sealed record InspectionInput(Guid PropertyId, Guid? UnitId, Guid? LeaseId, InspectionKind Kind, DateOnly InspectionDate, string? Inspector, string? Notes, IReadOnlyList<InspectionItemInput> Items);
public sealed record InspectionItemDto(Guid Id, string Area, ItemCondition Condition, string? Notes, decimal RepairCost, Guid? WorkOrderId);
public sealed record InspectionDto(Guid Id, Guid PropertyId, string PropertyName, Guid? UnitId, string? UnitNumber, Guid? LeaseId, InspectionKind Kind, DateOnly InspectionDate, string? Inspector, InspectionStatus Status, string? Notes, decimal DamageCost, IReadOnlyList<InspectionItemDto> Items);
public sealed record InspectionRow(Guid Id, Guid PropertyId, Guid? UnitId, Guid? LeaseId, InspectionKind Kind, DateOnly InspectionDate, string? Inspector, InspectionStatus Status, decimal DamageCost);

public sealed record GetInspection(Guid Id) : IQuery<InspectionDto>, IRequirePermission { public string RequiredPermission => P.InspectionsRead; }
public sealed record CreateInspection(InspectionInput Input) : ICommand<InspectionDto>, IRequirePermission { public string RequiredPermission => P.InspectionsManage; }
public sealed record UpdateInspection(Guid Id, InspectionInput Input) : ICommand<InspectionDto>, IRequirePermission { public string RequiredPermission => P.InspectionsManage; }
/// <summary>Finishes the inspection; with RaiseWorkOrders a repair work order is created for every poor/damaged item.</summary>
public sealed record CompleteInspection(Guid Id, bool RaiseWorkOrders) : ICommand<InspectionDto>, IRequirePermission { public string RequiredPermission => P.InspectionsManage; }

public sealed class InspectionInputValidator : AbstractValidator<InspectionInput>
{
    public InspectionInputValidator()
    {
        RuleFor(x => x.PropertyId).NotEmpty();
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Inspector).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(4000);
        RuleFor(x => x.Items).NotNull().Must(i => i is null || i.Count <= 200).WithMessage("At most 200 checklist items.");
        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(x => x.Area).NotEmpty().MaximumLength(100);
            i.RuleFor(x => x.Condition).IsInEnum();
            i.RuleFor(x => x.RepairCost).GreaterThanOrEqualTo(0);
            i.RuleFor(x => x.Notes).MaximumLength(1000);
        });
    }
}
public sealed class CreateInspectionValidator : AbstractValidator<CreateInspection> { public CreateInspectionValidator() => RuleFor(x => x.Input).SetValidator(new InspectionInputValidator()); }
public sealed class UpdateInspectionValidator : AbstractValidator<UpdateInspection> { public UpdateInspectionValidator() => RuleFor(x => x.Input).SetValidator(new InspectionInputValidator()); }

internal sealed class InspectionHandlers(IRepository<Inspection> inspections, IRepository<InspectionItem> itemRows, IReadRepository<Property> properties, IReadRepository<Unit> units, IReadRepository<Lease> leases,
    IRepository<WorkOrder> workOrders, INumberingService numbers, IUnitOfWork unitOfWork, IClock clock) :
    IRequestHandler<GetInspection, InspectionDto>,
    IRequestHandler<CreateInspection, InspectionDto>,
    IRequestHandler<UpdateInspection, InspectionDto>,
    IRequestHandler<CompleteInspection, InspectionDto>
{
    private DateOnly Today => DateOnly.FromDateTime(clock.UtcNow);

    public async Task<InspectionDto> Handle(GetInspection q, CancellationToken ct) => await ToDtoAsync(await LoadAsync(q.Id, false, ct), ct);

    public async Task<InspectionDto> Handle(CreateInspection cmd, CancellationToken ct)
    {
        var i = new Inspection();
        await MapAsync(cmd.Input, i, ct);
        i.Items.AddRange(Items(i, cmd.Input));
        i.DamageCost = Damage(i);
        inspections.Add(i);
        await inspections.SaveChangesAsync(ct);
        return await ToDtoAsync(i, ct);
    }

    public async Task<InspectionDto> Handle(UpdateInspection cmd, CancellationToken ct)
    {
        var i = await LoadAsync(cmd.Id, true, ct);
        if (i.Status == InspectionStatus.Completed) throw new ConflictException("A completed inspection can no longer be edited.");
        if (cmd.Input.PropertyId != i.PropertyId) throw new FieldValidationException("propertyId", "An inspection cannot move to another property.");
        await MapAsync(cmd.Input, i, ct);
        foreach (var old in i.Items.ToList()) itemRows.Remove(old);
        var fresh = Items(i, cmd.Input);
        itemRows.AddRange(fresh);
        i.DamageCost = fresh.Where(x => x.Condition >= ItemCondition.Poor).Sum(x => x.RepairCost);
        await inspections.SaveChangesAsync(ct);
        i.Items.Clear();
        i.Items.AddRange(fresh);
        return await ToDtoAsync(i, ct);
    }

    public Task<InspectionDto> Handle(CompleteInspection cmd, CancellationToken ct) => unitOfWork.InTransactionAsync(async token =>
    {
        var i = await LoadAsync(cmd.Id, true, token);
        if (i.Status == InspectionStatus.Completed) throw new ConflictException("This inspection is already completed.");
        if (i.Items.Count == 0) throw new ConflictException("Add at least one checklist item before completing.");
        if (cmd.RaiseWorkOrders)
            foreach (var item in i.Items.Where(x => x.Condition >= ItemCondition.Poor && x.WorkOrderId is null))
            {
                var w = new WorkOrder
                {
                    LegalEntityId = i.LegalEntityId, PropertyId = i.PropertyId, UnitId = i.UnitId, LeaseId = i.LeaseId, Issue = $"{item.Area}: {item.Condition} (inspection)", Category = "Inspection",
                    Priority = item.Condition == ItemCondition.Damaged ? Priority.High : Priority.Normal, ReportedOn = Today, ReportedBy = "Inspection", EstimatedCost = item.RepairCost, Details = item.Notes,
                };
                w.Number = await numbers.NextAsync(Docs.WorkOrder, w.LegalEntityId, Today, token);
                workOrders.Add(w);
                item.WorkOrderId = w.Id;
            }
        i.Status = InspectionStatus.Completed;
        await inspections.SaveChangesAsync(token);
        return await ToDtoAsync(i, token);
    }, ct);

    private async Task<Inspection> LoadAsync(Guid id, bool tracked, CancellationToken ct)
    {
        var spec = new Specification<Inspection>(x => x.Id == id).Include(x => x.Items);
        return await (tracked ? inspections.Tracked(spec) : inspections.Query(spec)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
    }

    private async Task MapAsync(InspectionInput i, Inspection x, CancellationToken ct)
    {
        var property = await properties.FindAsync(i.PropertyId, ct) ?? throw new FieldValidationException("propertyId", "Property not found.");
        if (i.UnitId is { } unitId && !await units.AnyAsync(u => u.Id == unitId && u.PropertyId == i.PropertyId, ct)) throw new FieldValidationException("unitId", "That unit is not in this property.");
        if (i.LeaseId is { } leaseId && !await leases.AnyAsync(l => l.Id == leaseId && l.PropertyId == i.PropertyId, ct)) throw new FieldValidationException("leaseId", "That lease is not in this property.");
        x.LegalEntityId = property.LegalEntityId; x.PropertyId = i.PropertyId; x.UnitId = i.UnitId; x.LeaseId = i.LeaseId; x.Kind = i.Kind;
        x.InspectionDate = i.InspectionDate; x.Inspector = i.Inspector; x.Notes = i.Notes;
    }

    private static List<InspectionItem> Items(Inspection i, InspectionInput input)
        => input.Items.Select(t => new InspectionItem { InspectionId = i.Id, Area = t.Area.Trim(), Condition = t.Condition, Notes = t.Notes, RepairCost = t.RepairCost }).ToList();

    private static decimal Damage(Inspection i) => i.Items.Where(x => x.Condition >= ItemCondition.Poor).Sum(x => x.RepairCost);

    private async Task<InspectionDto> ToDtoAsync(Inspection i, CancellationToken ct)
    {
        var property = await properties.GetAsync(i.PropertyId, ct);
        var unitNumber = i.UnitId is { } u ? (await units.FindAsync(u, ct))?.UnitNumber : null;
        return new(i.Id, i.PropertyId, property.Name, i.UnitId, unitNumber, i.LeaseId, i.Kind, i.InspectionDate, i.Inspector, i.Status, i.Notes, i.DamageCost,
            i.Items.Select(t => new InspectionItemDto(t.Id, t.Area, t.Condition, t.Notes, t.RepairCost, t.WorkOrderId)).ToList());
    }
}
