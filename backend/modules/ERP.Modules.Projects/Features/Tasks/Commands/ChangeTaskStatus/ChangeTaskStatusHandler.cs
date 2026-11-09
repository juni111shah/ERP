using ERP.Application.Common;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class ChangeTaskStatusHandler(
    IProjectTaskRepository tasks,
    IProjectRepository projects,
    TaskRules rules,
    TaskDtoBuilder dtos,
    ProjectAccess access)
    : ICommandHandler<ChangeTaskStatusCommand, TaskDto>
{
    public async Task<TaskDto> Handle(ChangeTaskStatusCommand command, CancellationToken ct)
    {
        // 1. Load it; the project must be open and the move allowed.
        var t = await tasks.GetForUpdateAsync(command.Id, ct);
        await access.EnsureProjectAsync(t.ProjectId, ct);
        ProjectRules.EnsureOpen(await projects.GetAsync(t.ProjectId, ct));
        await rules.CheckStatusAsync(t, command.Status, ct);

        // 2. Move it and save.
        t.Status = command.Status;
        await tasks.SaveChangesAsync(ct);
        return await dtos.BuildAsync(t, ct);
    }
}
