using ERP.Application.Common;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class UpdateTimesheetHandler(
    IProjectTimesheetRepository timesheets,
    IProjectTaskRepository tasks,
    TimesheetEntries entries,
    TimesheetDtoBuilder dtos)
    : ICommandHandler<UpdateTimesheetCommand, TimesheetDto>
{
    public async Task<TimesheetDto> Handle(UpdateTimesheetCommand command, CancellationToken ct)
    {
        // 1. Load, check and change the entry.
        var ts = await timesheets.GetForUpdateAsync(command.Id, ct);
        var oldTask = ts.TaskId;
        await entries.UpdateAsync(ts, command.Input, ct);
        await timesheets.SaveChangesAsync(ct);

        // 2. The actual hours of the task (and of the task it moved away from) follow the time logged.
        await TaskHours.RecalculateAsync(timesheets, tasks, ts.TaskId, ct);
        if (oldTask != ts.TaskId) await TaskHours.RecalculateAsync(timesheets, tasks, oldTask, ct);
        await timesheets.SaveChangesAsync(ct);
        return await dtos.BuildAsync(ts, ct);
    }
}
