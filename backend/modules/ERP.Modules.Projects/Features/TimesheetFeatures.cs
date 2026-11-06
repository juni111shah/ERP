using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.Projects.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Features;

/// <summary>
/// One entry of time. HourlyRate and CostRate may be left out: they then come from the rate cards. The worker is either a sign-in account
/// (WorkerUserId; the name is filled in) or a typed name. Id is only used by the weekly grid (<see cref="SaveTimesheetWeek"/>).
/// </summary>
public sealed record TimesheetInput(
    Guid ProjectId,
    Guid? TaskId,
    DateOnly Date,
    string? WorkerName,
    decimal Hours,
    decimal? HourlyRate,
    string? Description,
    bool IsBillable = true,
    Guid? WorkerUserId = null,
    decimal? CostRate = null,
    Guid? Id = null);

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
    DateTime CreatedAt,
    Guid? WorkerUserId = null,
    decimal CostRate = 0m,
    decimal CostAmount = 0m,
    TimesheetStatus ApprovalStatus = TimesheetStatus.Approved,
    string? RejectReason = null,
    bool WrittenOff = false);

public sealed record GetTimesheet(Guid Id) : IQuery<TimesheetDto>, IRequirePermission { public string RequiredPermission => P.TimesheetsRead; }
public sealed record CreateTimesheet(TimesheetInput Input) : ICommand<TimesheetDto>, IRequirePermission { public string RequiredPermission => P.TimesheetsManage; }
public sealed record UpdateTimesheet(Guid Id, TimesheetInput Input) : ICommand<TimesheetDto>, IRequirePermission { public string RequiredPermission => P.TimesheetsManage; }
public sealed record DeleteTimesheet(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.TimesheetsManage; }
/// <summary>Creates, changes or removes (Hours 0 with an Id) many entries at once, for the weekly grid.</summary>
public sealed record SaveTimesheetWeek(IReadOnlyList<TimesheetInput> Items) : ICommand<IReadOnlyList<TimesheetDto>>, IRequirePermission { public string RequiredPermission => P.TimesheetsManage; }
public sealed record SubmitTimesheets(IReadOnlyList<Guid> Ids) : ICommand<int>, IRequirePermission { public string RequiredPermission => P.TimesheetsManage; }
public sealed record DecideTimesheets(IReadOnlyList<Guid> Ids, bool Approve, string? Reason) : ICommand<int>, IRequirePermission { public string RequiredPermission => P.TimesheetsApprove; }
/// <summary>Marks unbilled time as not to be charged; it leaves the unbilled figures and cannot be billed.</summary>
public sealed record WriteOffTimesheets(IReadOnlyList<Guid> Ids) : ICommand<int>, IRequirePermission { public string RequiredPermission => P.BillingManage; }

public sealed class TimesheetInputValidator : AbstractValidator<TimesheetInput>
{
    public TimesheetInputValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.WorkerName).NotEmpty().When(x => x.WorkerUserId is null).WithMessage("Say who did the work.");
        RuleFor(x => x.WorkerName).MaximumLength(100);
        RuleFor(x => x.Hours).GreaterThan(0).LessThanOrEqualTo(24).WithMessage("Hours must be between 0.1 and 24.");
        RuleFor(x => x.HourlyRate).GreaterThanOrEqualTo(0).When(x => x.HourlyRate != null);
        RuleFor(x => x.CostRate).GreaterThanOrEqualTo(0).When(x => x.CostRate != null);
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

public sealed class SaveTimesheetWeekValidator : AbstractValidator<SaveTimesheetWeek>
{
    public SaveTimesheetWeekValidator()
    {
        RuleFor(x => x.Items).NotEmpty().Must(i => i.Count <= 200).WithMessage("Save at most 200 entries at a time.");
        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(x => x.ProjectId).NotEmpty();
            i.RuleFor(x => x.Hours).InclusiveBetween(0, 24);
            i.RuleFor(x => x.WorkerName).MaximumLength(100);
            i.RuleFor(x => x.Description).MaximumLength(500);
        });
    }
}

public sealed class SubmitTimesheetsValidator : AbstractValidator<SubmitTimesheets> { public SubmitTimesheetsValidator() => RuleFor(x => x.Ids).NotEmpty().Must(i => i.Count <= 500); }
public sealed class DecideTimesheetsValidator : AbstractValidator<DecideTimesheets>
{
    public DecideTimesheetsValidator()
    {
        RuleFor(x => x.Ids).NotEmpty().Must(i => i.Count <= 500);
        RuleFor(x => x.Reason).NotEmpty().When(x => !x.Approve).WithMessage("Say why the time is rejected.");
        RuleFor(x => x.Reason).MaximumLength(300);
    }
}
public sealed class WriteOffTimesheetsValidator : AbstractValidator<WriteOffTimesheets> { public WriteOffTimesheetsValidator() => RuleFor(x => x.Ids).NotEmpty().Must(i => i.Count <= 500); }

internal sealed class TimesheetHandlers(
    IRepository<ProjectTimesheet> timesheets,
    IReadRepository<Project> projects,
    IRepository<ProjectTask> tasks,
    ProjectPeople people,
    ProjectAccess access,
    RateResolver rates,
    ISettingsService settings,
    IReadRepository<ProjectMember> members,
    ICurrentUser user,
    ERP.Application.Finance.IPostingService posting,
    IUnitOfWork uow) :
    IRequestHandler<GetTimesheet, TimesheetDto>,
    IRequestHandler<CreateTimesheet, TimesheetDto>,
    IRequestHandler<UpdateTimesheet, TimesheetDto>,
    IRequestHandler<DeleteTimesheet, Unit>,
    IRequestHandler<SaveTimesheetWeek, IReadOnlyList<TimesheetDto>>,
    IRequestHandler<SubmitTimesheets, int>,
    IRequestHandler<DecideTimesheets, int>,
    IRequestHandler<WriteOffTimesheets, int>
{
    public async Task<TimesheetDto> Handle(GetTimesheet q, CancellationToken ct)
    {
        var ts = await timesheets.GetAsync(q.Id, ct);
        await access.EnsureProjectAsync(ts.ProjectId, ct);
        return await BuildDtoAsync(ts, ct);
    }

    /// <summary>People change their own time; changing someone else's takes approval or project-manager rights. A typed name has no owner.</summary>
    private void EnsureMayChange(ProjectTimesheet ts)
    {
        if (ts.WorkerUserId is null || ts.WorkerUserId == user.UserId) return;
        if (access.Has(P.TimesheetsApprove) || access.Has(P.ProjectsManage)) return;
        throw new ForbiddenException("Only the person who did the work or a manager can change this time.");
    }

    public async Task<TimesheetDto> Handle(CreateTimesheet cmd, CancellationToken ct)
    {
        var ts = await CreateCoreAsync(cmd.Input, ct);
        await timesheets.SaveChangesAsync(ct);
        await TaskHours.RecalculateAsync(timesheets, tasks, ts.TaskId, ct);
        await timesheets.SaveChangesAsync(ct);
        return await BuildDtoAsync(ts, ct);
    }

    public async Task<TimesheetDto> Handle(UpdateTimesheet cmd, CancellationToken ct)
    {
        var ts = await timesheets.GetForUpdateAsync(cmd.Id, ct);
        var oldTask = ts.TaskId;
        await UpdateCoreAsync(ts, cmd.Input, ct);
        await timesheets.SaveChangesAsync(ct);
        await TaskHours.RecalculateAsync(timesheets, tasks, ts.TaskId, ct);
        if (oldTask != ts.TaskId) await TaskHours.RecalculateAsync(timesheets, tasks, oldTask, ct);
        await timesheets.SaveChangesAsync(ct);
        return await BuildDtoAsync(ts, ct);
    }

    public async Task<Unit> Handle(DeleteTimesheet cmd, CancellationToken ct)
    {
        var ts = await timesheets.GetForUpdateAsync(cmd.Id, ct);
        await DeleteCoreAsync(ts, ct);
        await timesheets.SaveChangesAsync(ct);
        await TaskHours.RecalculateAsync(timesheets, tasks, ts.TaskId, ct);
        await timesheets.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public Task<IReadOnlyList<TimesheetDto>> Handle(SaveTimesheetWeek cmd, CancellationToken ct) => uow.InTransactionAsync<IReadOnlyList<TimesheetDto>>(async token =>
    {
        var touched = new List<ProjectTimesheet>(); var taskIds = new HashSet<Guid>();
        foreach (var i in cmd.Items)
        {
            if (i.Id is { } id)
            {
                var existing = await timesheets.GetForUpdateAsync(id, token);
                if (existing.TaskId is { } t0) taskIds.Add(t0);
                if (i.Hours == 0) { await DeleteCoreAsync(existing, token); await timesheets.SaveChangesAsync(token); continue; }
                // the grid only changes hours: everything else on the entry stays as it was
                await UpdateCoreAsync(existing, i with { Description = i.Description ?? existing.Description, HourlyRate = i.HourlyRate ?? existing.HourlyRate, CostRate = i.CostRate ?? existing.CostRate, IsBillable = existing.IsBillable, WorkerUserId = existing.WorkerUserId, WorkerName = existing.WorkerName, TaskId = existing.TaskId }, token);
                touched.Add(existing);
            }
            else if (i.Hours > 0) touched.Add(await CreateCoreAsync(i, token));
            await timesheets.SaveChangesAsync(token);
        }
        foreach (var t in touched) if (t.TaskId is { } tid) taskIds.Add(tid);
        foreach (var tid in taskIds) await TaskHours.RecalculateAsync(timesheets, tasks, tid, token);
        await timesheets.SaveChangesAsync(token);
        var result = new List<TimesheetDto>();
        foreach (var t in touched) result.Add(await BuildDtoAsync(t, token));
        return result;
    }, ct);

    public async Task<int> Handle(SubmitTimesheets cmd, CancellationToken ct)
    {
        var rows = await timesheets.Tracked(new Specification<ProjectTimesheet>(t => cmd.Ids.Contains(t.Id))).ToListAsync(ct);
        if (rows.Count != cmd.Ids.Distinct().Count()) throw new NotFoundException("One of the entries was not found.");
        await access.EnsureProjectsAsync(rows.Select(t => t.ProjectId), ct);
        foreach (var t in rows) EnsureMayChange(t);
        var n = 0;
        foreach (var t in rows)
        {
            if (t.ApprovalStatus is TimesheetStatus.Submitted or TimesheetStatus.Approved) continue;
            t.ApprovalStatus = TimesheetStatus.Submitted; t.SubmittedAt = ERP.Domain.Common.AppClock.UtcNow; t.RejectReason = null; n++;
        }
        await timesheets.SaveChangesAsync(ct);
        return n;
    }

    public async Task<int> Handle(DecideTimesheets cmd, CancellationToken ct)
    {
        var rows = await timesheets.Tracked(new Specification<ProjectTimesheet>(t => cmd.Ids.Contains(t.Id))).ToListAsync(ct);
        if (rows.Count != cmd.Ids.Distinct().Count()) throw new NotFoundException("One of the entries was not found.");
        await access.EnsureProjectsAsync(rows.Select(t => t.ProjectId), ct);
        if (cmd.Approve && user.UserId is { } me && rows.Any(t => t.WorkerUserId == me && t.ApprovalStatus == TimesheetStatus.Submitted)
            && await settings.GetAsync<bool?>("projects.allowSelfApproval", null, ct) != true)
            throw new ConflictException("You cannot approve your own time. Ask another approver.");
        var n = 0;
        foreach (var t in rows)
        {
            if (t.ApprovalStatus != TimesheetStatus.Submitted) continue;
            t.ApprovalStatus = cmd.Approve ? TimesheetStatus.Approved : TimesheetStatus.Rejected;
            t.RejectReason = cmd.Approve ? null : cmd.Reason?.Trim();
            t.DecidedAt = ERP.Domain.Common.AppClock.UtcNow; t.DecidedByUserId = user.UserId; n++;
        }
        await timesheets.SaveChangesAsync(ct);
        return n;
    }

    public Task<int> Handle(WriteOffTimesheets cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var rows = await timesheets.Tracked(new Specification<ProjectTimesheet>(t => cmd.Ids.Contains(t.Id))).ToListAsync(token);
        if (rows.Count != cmd.Ids.Distinct().Count()) throw new NotFoundException("One of the entries was not found.");
        if (rows.Any(t => t.IsBilled)) throw new ConflictException("Billed time cannot be written off. Void the invoice first.");
        await access.EnsureProjectsAsync(rows.Select(t => t.ProjectId), token);
        foreach (var group in rows.GroupBy(t => t.ProjectId))
        {
            var posted = group.Where(t => t.CostJournalId != null && t.WipReleaseJournalId == null).ToList();
            var p = await projects.GetAsync(group.Key, token);
            var journal = await ProjectWip.ReleaseAsync(posting, p, posted.Sum(t => t.CostAmount), p.Number, $"Project {p.Number} work written off, work in progress to cost of sales", token);
            if (journal is not null) foreach (var t in posted) t.WipReleaseJournalId = journal.Id;
        }
        foreach (var t in rows) t.WrittenOff = true;
        await timesheets.SaveChangesAsync(token);
        return rows.Count;
    }, ct);

    private async Task<ProjectTimesheet> CreateCoreAsync(TimesheetInput i, CancellationToken ct)
    {
        var p = await projects.GetAsync(i.ProjectId, ct);
        ProjectRules.EnsureTrackable(p);
        await access.EnsureAsync(p, ct);
        await EnsureTaskInProjectAsync(i.TaskId, p.Id, ct);
        var (name, userId) = await WorkerAsync(i, ct);
        await EnsureDayAsync(p, i, name, userId, null, ct);
        var (bill, cost) = await RatesAsync(p, i, userId, ct);
        var approval = await settings.GetAsync<bool?>("projects.timesheetApproval", p.LegalEntityId, ct) == true;
        var ts = new ProjectTimesheet
        {
            ProjectId = p.Id, TaskId = i.TaskId, Date = i.Date, WorkerName = name, WorkerUserId = userId, Hours = i.Hours,
            HourlyRate = bill, TotalAmount = ProjectMath.Round(i.Hours * bill), CostRate = cost, CostAmount = ProjectMath.Round(i.Hours * cost),
            Description = i.Description?.Trim(), IsBillable = i.IsBillable && p.BillingType != ProjectBillingType.NonBillable, IsBilled = false,
            ApprovalStatus = approval ? TimesheetStatus.Draft : TimesheetStatus.Approved,
        };
        timesheets.Add(ts);
        return ts;
    }

    private async Task UpdateCoreAsync(ProjectTimesheet ts, TimesheetInput i, CancellationToken ct)
    {
        if (ts.IsBilled) throw new ConflictException("Cannot edit a billed timesheet.");
        if (ts.WrittenOff) throw new ConflictException("Cannot edit time that was written off.");
        if (ts.CostJournalId != null) throw new ConflictException("The cost of this time is already in the ledger, so it cannot be changed. Write it off or log a correcting entry.");
        var p = await projects.GetAsync(ts.ProjectId, ct);
        ProjectRules.EnsureTrackable(p);
        await access.EnsureAsync(p, ct);
        EnsureMayChange(ts);
        if (ts.ApprovalStatus is TimesheetStatus.Submitted or TimesheetStatus.Approved && await settings.GetAsync<bool?>("projects.timesheetApproval", p.LegalEntityId, ct) == true)
            throw new ConflictException("Submitted or approved time cannot be edited. Ask for it to be rejected first.");
        await EnsureTaskInProjectAsync(i.TaskId, ts.ProjectId, ct);
        var (name, userId) = await WorkerAsync(i, ct);
        await EnsureDayAsync(p, i, name, userId, ts.Id, ct);
        // the cost of the hour stays what it was unless the person changed
        var (bill, cost) = await RatesAsync(p, userId == ts.WorkerUserId ? i with { CostRate = i.CostRate ?? ts.CostRate } : i, userId, ct, ts);
        ts.TaskId = i.TaskId; ts.Date = i.Date; ts.WorkerName = name; ts.WorkerUserId = userId; ts.Hours = i.Hours;
        ts.HourlyRate = bill; ts.TotalAmount = ProjectMath.Round(i.Hours * bill); ts.CostRate = cost; ts.CostAmount = ProjectMath.Round(i.Hours * cost);
        ts.Description = i.Description?.Trim(); ts.IsBillable = i.IsBillable && p.BillingType != ProjectBillingType.NonBillable;
        if (ts.ApprovalStatus == TimesheetStatus.Rejected) ts.ApprovalStatus = TimesheetStatus.Draft;
    }

    private async Task DeleteCoreAsync(ProjectTimesheet ts, CancellationToken ct)
    {
        if (ts.IsBilled) throw new ConflictException("Cannot delete a billed timesheet.");
        if (ts.CostJournalId != null) throw new ConflictException("The cost of this time is already in the ledger, so it cannot be deleted. Write it off instead.");
        var p = await projects.GetAsync(ts.ProjectId, ct);
        ProjectRules.EnsureTrackable(p);
        await access.EnsureAsync(p, ct);
        EnsureMayChange(ts);
        if (ts.ApprovalStatus == TimesheetStatus.Submitted) throw new ConflictException("Submitted time cannot be deleted. Ask for it to be rejected first.");
        if (ts.ApprovalStatus == TimesheetStatus.Approved && await settings.GetAsync<bool?>("projects.timesheetApproval", p.LegalEntityId, ct) == true)
            throw new ConflictException("Approved time cannot be deleted. Ask for it to be rejected first.");
        timesheets.Remove(ts);
    }

    private async Task<(string Name, Guid? UserId)> WorkerAsync(TimesheetInput i, CancellationToken ct)
    {
        if (i.WorkerUserId is { } uid) return (await people.RequireNameAsync(uid, "workerUserId", ct), uid);
        return (i.WorkerName!.Trim(), null);
    }

    private async Task<(decimal Bill, decimal Cost)> RatesAsync(Project p, TimesheetInput i, Guid? userId, CancellationToken ct, ProjectTimesheet? existing = null)
    {
        if (i.HourlyRate is { } b && i.CostRate is { } c) return (b, c);
        string? role = userId is { } u ? (await members.Query(new Specification<ProjectMember>(m => m.ProjectId == p.Id && m.UserId == u)).Select(m => m.Role).FirstOrDefaultAsync(ct)) : null;
        var (rb, rc) = await rates.ResolveAsync(p.Id, userId, role, p.LegalEntityId, ct);
        return (i.HourlyRate ?? rb, i.CostRate ?? rc);
    }

    /// <summary>No future dates unless the setting allows them, and one person cannot log more than the daily limit across all entries.</summary>
    private async Task EnsureDayAsync(Project p, TimesheetInput i, string name, Guid? userId, Guid? exceptId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow);
        if (i.Date > today.AddDays(1) && await settings.GetAsync<bool?>("projects.allowFutureTime", p.LegalEntityId, ct) != true)
            throw new FieldValidationException("date", "Time cannot be logged for a future date.");
        var limit = await settings.GetAsync<decimal?>("projects.maxHoursPerDay", p.LegalEntityId, ct) ?? 16m;
        var date = i.Date;
        var others = timesheets.Query(new Specification<ProjectTimesheet>(t => t.Date == date && (exceptId == null || t.Id != exceptId)));
        var already = userId is { } uid
            ? await others.Where(t => t.WorkerUserId == uid).SumDecimalAsync(t => t.Hours, ct)
            : await others.Where(t => t.WorkerUserId == null && t.WorkerName == name).SumDecimalAsync(t => t.Hours, ct);
        if (already + i.Hours > limit)
            throw new FieldValidationException("hours", $"{name} would have {already + i.Hours:0.##} hours on {date:yyyy-MM-dd}; the limit is {limit:0.##} a day.");
    }

    private async Task EnsureTaskInProjectAsync(Guid? taskId, Guid projectId, CancellationToken ct)
    {
        if (taskId is { } tid && !await tasks.AnyAsync(t => t.Id == tid && t.ProjectId == projectId, ct))
            throw new FieldValidationException("taskId", "Task not found in this project.");
    }

    private async Task<TimesheetDto> BuildDtoAsync(ProjectTimesheet ts, CancellationToken ct)
    {
        var p = await projects.GetAsync(ts.ProjectId, ct);
        string? tTitle = null;
        if (ts.TaskId is { } tid)
            tTitle = await tasks.Query(new Specification<ProjectTask>(x => x.Id == tid)).Select(x => x.Title).FirstOrDefaultAsync(ct);
        var cost = access.CanSeeCost;
        return new TimesheetDto(ts.Id, ts.ProjectId, p.Name, ts.TaskId, tTitle, ts.Date, ts.WorkerName, ts.Hours, ts.HourlyRate, ts.TotalAmount, ts.Description, ts.IsBillable, ts.IsBilled, ts.InvoiceId, ts.CreatedAt,
            ts.WorkerUserId, cost ? ts.CostRate : 0m, cost ? ts.CostAmount : 0m, ts.ApprovalStatus, ts.RejectReason, ts.WrittenOff);
    }
}
