using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class DecideTimesheetsHandler(
    IProjectTimesheetRepository timesheets,
    ProjectAccess access,
    ISettingsService settings,
    ICurrentUser user)
    : ICommandHandler<DecideTimesheetsCommand, int>
{
    public async Task<int> Handle(DecideTimesheetsCommand command, CancellationToken ct)
    {
        // 1. Every entry must exist; nobody approves their own time unless the workspace allows it.
        var rows = await timesheets.ListByIdsForUpdateAsync(command.Ids, ct);
        if (rows.Count != command.Ids.Distinct().Count()) throw new NotFoundException("One of the entries was not found.");
        await access.EnsureProjectsAsync(rows.Select(t => t.ProjectId), ct);
        if (command.Approve && user.UserId is { } me && rows.Any(t => t.WorkerUserId == me && t.ApprovalStatus == TimesheetStatus.Submitted)
            && await settings.GetAsync<bool?>("projects.allowSelfApproval", null, ct) != true)
            throw new ConflictException("You cannot approve your own time. Ask another approver.");

        // 2. Decide the submitted ones and save.
        var n = 0;
        foreach (var t in rows)
        {
            if (t.ApprovalStatus != TimesheetStatus.Submitted) continue;
            t.ApprovalStatus = command.Approve ? TimesheetStatus.Approved : TimesheetStatus.Rejected;
            t.RejectReason = command.Approve ? null : command.Reason?.Trim();
            t.DecidedAt = ERP.Domain.Common.AppClock.UtcNow; t.DecidedByUserId = user.UserId; n++;
        }
        await timesheets.SaveChangesAsync(ct);
        return n;
    }
}
