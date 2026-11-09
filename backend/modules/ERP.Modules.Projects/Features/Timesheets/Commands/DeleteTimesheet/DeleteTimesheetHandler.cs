using ERP.Application.Common;
using ERP.Modules.Projects.Repositories;
using MediatR;

namespace ERP.Modules.Projects.Features;

internal sealed class DeleteTimesheetHandler(
    IProjectTimesheetRepository timesheets,
    IProjectTaskRepository tasks,
    TimesheetEntries entries)
    : ICommandHandler<DeleteTimesheetCommand>
{
    public async Task<Unit> Handle(DeleteTimesheetCommand command, CancellationToken ct)
    {
        // 1. Load, check and remove the entry.
        var ts = await timesheets.GetForUpdateAsync(command.Id, ct);
        await entries.DeleteAsync(ts, ct);
        await timesheets.SaveChangesAsync(ct);

        // 2. The task's actual hours follow the time logged against it.
        await TaskHours.RecalculateAsync(timesheets, tasks, ts.TaskId, ct);
        await timesheets.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
