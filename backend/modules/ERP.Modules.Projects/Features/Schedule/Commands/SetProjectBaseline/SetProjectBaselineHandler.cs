using ERP.Application.Common;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class SetProjectBaselineHandler(ProjectScheduleBuilder schedules, IProjectTaskRepository tasks) : ICommandHandler<SetProjectBaselineCommand, ScheduleDto>
{
    public async Task<ScheduleDto> Handle(SetProjectBaselineCommand command, CancellationToken ct)
    {
        // 1. The current plan of every task.
        var schedule = await schedules.BuildAsync(command.ProjectId, ct);
        var slots = schedule.Tasks.ToDictionary(t => t.Id);

        // 2. Copy it onto the tasks as their baseline (or clear it) and save.
        var id = command.ProjectId;
        foreach (var t in await tasks.ListOfProjectForUpdateAsync(id, ct))
        {
            var slot = slots.GetValueOrDefault(t.Id);
            t.BaselineStart = command.Clear ? null : slot?.Start;
            t.BaselineEnd = command.Clear ? null : slot?.End;
        }
        await tasks.SaveChangesAsync(ct);

        // 3. Answer with the schedule as it now is.
        return await schedules.BuildAsync(command.ProjectId, ct);
    }
}
