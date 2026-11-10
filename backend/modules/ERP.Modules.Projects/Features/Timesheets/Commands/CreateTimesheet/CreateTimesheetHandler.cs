using ERP.Application.Common;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class CreateTimesheetHandler(
    IProjectTimesheetRepository timesheets,
    IProjectTaskRepository tasks,
    TimesheetEntries entries,
    TimesheetDtoBuilder dtos)
    : ICommandHandler<CreateTimesheetCommand, TimesheetDto>
{
    public async Task<TimesheetDto> Handle(CreateTimesheetCommand command, CancellationToken ct)
    {
        // 1. Check and add the entry.
        var ts = await entries.CreateAsync(command.Input, ct);
        await timesheets.SaveChangesAsync(ct);

        // 2. The task's actual hours follow the time logged against it.
        await TaskHours.RecalculateAsync(timesheets, tasks, ts.TaskId, ct);
        await timesheets.SaveChangesAsync(ct);
        return await dtos.BuildAsync(ts, ct);
    }
}
