using ERP.Application.Common;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class UpdateTaskHandler(
    IProjectTaskRepository tasks,
    IProjectRepository projects,
    TaskRules rules,
    TaskDtoBuilder dtos,
    ProjectAccess access)
    : ICommandHandler<UpdateTaskCommand, TaskDto>
{
    public async Task<TaskDto> Handle(UpdateTaskCommand command, CancellationToken ct)
    {
        // 1. Load it; the project must be open and the milestone and the parent must belong to it.
        var t = await tasks.GetForUpdateAsync(command.Id, ct);
        var i = command.Input;
        await access.EnsureProjectAsync(t.ProjectId, ct);
        ProjectRules.EnsureOpen(await projects.GetAsync(t.ProjectId, ct));
        await rules.EnsureMilestoneInProjectAsync(i.MilestoneId, t.ProjectId, ct);
        await rules.EnsureParentAsync(i.ParentTaskId, t.ProjectId, t.Id, ct);

        // 2. A status change must be an allowed move.
        if (i.Status != t.Status) await rules.CheckStatusAsync(t, i.Status, ct);

        // 3. Apply the form and save.
        await rules.FillAsync(t, i, ct);
        await tasks.SaveChangesAsync(ct);
        return await dtos.BuildAsync(t, ct);
    }
}
