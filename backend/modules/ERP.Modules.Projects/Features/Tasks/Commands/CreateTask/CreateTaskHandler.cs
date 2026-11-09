using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class CreateTaskHandler(
    IProjectTaskRepository tasks,
    IProjectRepository projects,
    TaskRules rules,
    TaskDtoBuilder dtos,
    ProjectAccess access,
    INumberingService numbers)
    : ICommandHandler<CreateTaskCommand, TaskDto>
{
    public async Task<TaskDto> Handle(CreateTaskCommand command, CancellationToken ct)
    {
        // 1. The project must be open; the milestone and the parent must belong to it.
        var i = command.Input;
        var p = await projects.GetAsync(i.ProjectId, ct);
        ProjectRules.EnsureOpen(p);
        await access.EnsureAsync(p, ct);
        await rules.EnsureMilestoneInProjectAsync(i.MilestoneId, p.Id, ct);
        await rules.EnsureParentAsync(i.ParentTaskId, p.Id, null, ct);

        // 2. Number it, fill it in and save.
        var t = new ProjectTask { ProjectId = p.Id, Number = await numbers.NextAsync("task", p.LegalEntityId, null, ct) };
        await rules.FillAsync(t, i, ct);
        tasks.Add(t);
        await tasks.SaveChangesAsync(ct);
        return await dtos.BuildAsync(t, ct);
    }
}
