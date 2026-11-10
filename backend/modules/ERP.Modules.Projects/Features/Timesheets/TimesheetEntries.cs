using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

/// <summary>
/// Creating, changing and removing one time entry, with every check that goes with it (the project, the worker, the rates and the daily limit).
/// Shared by the single-entry handlers and the weekly grid. The caller saves.
/// </summary>
public sealed class TimesheetEntries(
    IProjectTimesheetRepository timesheets,
    IProjectRepository projects,
    IProjectTaskRepository tasks,
    IProjectMemberRepository members,
    ProjectPeople people,
    ProjectAccess access,
    RateResolver rates,
    ISettingsService settings,
    ICurrentUser user)
{
    /// <summary>People change their own time; changing someone else's takes approval or project-manager rights. A typed name has no owner.</summary>
    public void EnsureMayChange(ProjectTimesheet ts)
    {
        if (ts.WorkerUserId is null || ts.WorkerUserId == user.UserId) return;
        if (access.Has(P.TimesheetsApprove) || access.Has(P.ProjectsManage)) return;
        throw new ForbiddenException("Only the person who did the work or a manager can change this time.");
    }

    public async Task<ProjectTimesheet> CreateAsync(TimesheetInput i, CancellationToken ct)
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

    public async Task UpdateAsync(ProjectTimesheet ts, TimesheetInput i, CancellationToken ct)
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

    public async Task DeleteAsync(ProjectTimesheet ts, CancellationToken ct)
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
        string? role = userId is { } u ? (await members.GetRoleAsync(p.Id, u, ct)) : null;
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
        var already = userId is { } uid
            ? await timesheets.HoursOnDayOfUserAsync(date, exceptId, uid, ct)
            : await timesheets.HoursOnDayOfNameAsync(date, exceptId, name, ct);
        if (already + i.Hours > limit)
            throw new FieldValidationException("hours", $"{name} would have {already + i.Hours:0.##} hours on {date:yyyy-MM-dd}; the limit is {limit:0.##} a day.");
    }

    private async Task EnsureTaskInProjectAsync(Guid? taskId, Guid projectId, CancellationToken ct)
    {
        if (taskId is { } tid && !await tasks.AnyAsync(t => t.Id == tid && t.ProjectId == projectId, ct))
            throw new FieldValidationException("taskId", "Task not found in this project.");
    }
}
