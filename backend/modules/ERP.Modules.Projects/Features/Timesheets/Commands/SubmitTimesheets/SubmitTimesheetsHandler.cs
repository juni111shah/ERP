using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class SubmitTimesheetsHandler(IProjectTimesheetRepository timesheets, TimesheetEntries entries, ProjectAccess access) : ICommandHandler<SubmitTimesheetsCommand, int>
{
    public async Task<int> Handle(SubmitTimesheetsCommand command, CancellationToken ct)
    {
        // 1. Every entry must exist and be one the caller may change.
        var rows = await timesheets.ListByIdsForUpdateAsync(command.Ids, ct);
        if (rows.Count != command.Ids.Distinct().Count()) throw new NotFoundException("One of the entries was not found.");
        await access.EnsureProjectsAsync(rows.Select(t => t.ProjectId), ct);
        foreach (var t in rows) entries.EnsureMayChange(t);

        // 2. Submit the ones that are not submitted or approved yet, and save.
        var n = 0;
        foreach (var t in rows)
        {
            if (t.ApprovalStatus is TimesheetStatus.Submitted or TimesheetStatus.Approved) continue;
            t.ApprovalStatus = TimesheetStatus.Submitted; t.SubmittedAt = ERP.Domain.Common.AppClock.UtcNow; t.RejectReason = null; n++;
        }
        await timesheets.SaveChangesAsync(ct);
        return n;
    }
}
