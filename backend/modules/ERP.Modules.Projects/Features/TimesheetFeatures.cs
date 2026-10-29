using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.Projects.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Features;

public sealed record TimesheetInput(
    Guid ProjectId,
    Guid? TaskId,
    DateOnly Date,
    string WorkerName,
    decimal Hours,
    decimal HourlyRate,
    string? Description,
    bool IsBillable = true);

public sealed record TimesheetDto(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    Guid? TaskId,
    string? TaskTitle,
    DateOnly Date,
    string WorkerName,
    decimal Hours,
    decimal HourlyRate,
    decimal TotalAmount,
    string? Description,
    bool IsBillable,
    bool IsBilled,
    Guid? InvoiceId,
    DateTime CreatedAt);

public sealed record GetTimesheet(Guid Id) : IQuery<TimesheetDto>, IRequirePermission { public string RequiredPermission => P.TimesheetsRead; }
public sealed record CreateTimesheet(TimesheetInput Input) : ICommand<TimesheetDto>, IRequirePermission { public string RequiredPermission => P.TimesheetsManage; }
public sealed record UpdateTimesheet(Guid Id, TimesheetInput Input) : ICommand<TimesheetDto>, IRequirePermission { public string RequiredPermission => P.TimesheetsManage; }
public sealed record DeleteTimesheet(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.TimesheetsManage; }

public sealed class TimesheetInputValidator : AbstractValidator<TimesheetInput>
{
    public TimesheetInputValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.WorkerName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Hours).GreaterThan(0).LessThanOrEqualTo(24).WithMessage("Hours must be between 0.1 and 24.");
        RuleFor(x => x.HourlyRate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public sealed class CreateTimesheetValidator : AbstractValidator<CreateTimesheet>
{
    public CreateTimesheetValidator() => RuleFor(x => x.Input).SetValidator(new TimesheetInputValidator());
}

public sealed class UpdateTimesheetValidator : AbstractValidator<UpdateTimesheet>
{
    public UpdateTimesheetValidator() => RuleFor(x => x.Input).SetValidator(new TimesheetInputValidator());
}

internal sealed class TimesheetHandlers(
    IRepository<ProjectTimesheet> timesheets,
    IReadRepository<Project> projects,
    IReadRepository<ProjectTask> tasks) :
    IRequestHandler<GetTimesheet, TimesheetDto>,
    IRequestHandler<CreateTimesheet, TimesheetDto>,
    IRequestHandler<UpdateTimesheet, TimesheetDto>,
    IRequestHandler<DeleteTimesheet, Unit>
{
    public async Task<TimesheetDto> Handle(GetTimesheet q, CancellationToken ct)
    {
        var ts = await timesheets.GetAsync(q.Id, ct);
        return await BuildDtoAsync(ts, ct);
    }

    public async Task<TimesheetDto> Handle(CreateTimesheet cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var p = await projects.GetAsync(i.ProjectId, ct);
        var ts = new ProjectTimesheet
        {
            ProjectId = p.Id,
            TaskId = i.TaskId,
            Date = i.Date,
            WorkerName = i.WorkerName.Trim(),
            Hours = i.Hours,
            HourlyRate = i.HourlyRate,
            TotalAmount = Math.Round(i.Hours * i.HourlyRate, 4),
            Description = i.Description?.Trim(),
            IsBillable = i.IsBillable,
            IsBilled = false,
        };
        timesheets.Add(ts);
        await timesheets.SaveChangesAsync(ct);
        return await BuildDtoAsync(ts, ct);
    }

    public async Task<TimesheetDto> Handle(UpdateTimesheet cmd, CancellationToken ct)
    {
        var ts = await timesheets.GetForUpdateAsync(cmd.Id, ct);
        if (ts.IsBilled) throw new ConflictException("Cannot edit a billed timesheet.");
        var i = cmd.Input;
        ts.TaskId = i.TaskId;
        ts.Date = i.Date;
        ts.WorkerName = i.WorkerName.Trim();
        ts.Hours = i.Hours;
        ts.HourlyRate = i.HourlyRate;
        ts.TotalAmount = Math.Round(i.Hours * i.HourlyRate, 4);
        ts.Description = i.Description?.Trim();
        ts.IsBillable = i.IsBillable;

        await timesheets.SaveChangesAsync(ct);
        return await BuildDtoAsync(ts, ct);
    }

    public async Task<Unit> Handle(DeleteTimesheet cmd, CancellationToken ct)
    {
        var ts = await timesheets.GetForUpdateAsync(cmd.Id, ct);
        if (ts.IsBilled) throw new ConflictException("Cannot delete a billed timesheet.");
        timesheets.Remove(ts);
        await timesheets.SaveChangesAsync(ct);
        return Unit.Value;
    }

    private async Task<TimesheetDto> BuildDtoAsync(ProjectTimesheet ts, CancellationToken ct)
    {
        var p = await projects.GetAsync(ts.ProjectId, ct);
        string? tTitle = null;
        if (ts.TaskId.HasValue)
        {
            var t = await tasks.Query(new Specification<ProjectTask>(x => x.Id == ts.TaskId.Value)).FirstOrDefaultAsync(ct);
            tTitle = t?.Title;
        }

        return new TimesheetDto(
            ts.Id,
            ts.ProjectId,
            p.Name,
            ts.TaskId,
            tTitle,
            ts.Date,
            ts.WorkerName,
            ts.Hours,
            ts.HourlyRate,
            ts.TotalAmount,
            ts.Description,
            ts.IsBillable,
            ts.IsBilled,
            ts.InvoiceId,
            ts.CreatedAt);
    }
}
