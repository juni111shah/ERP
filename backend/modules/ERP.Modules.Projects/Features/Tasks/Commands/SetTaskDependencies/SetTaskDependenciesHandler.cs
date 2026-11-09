using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class SetTaskDependenciesHandler(
    IProjectTaskRepository tasks,
    IProjectRepository projects,
    IProjectTaskDependencyRepository dependencies,
    TaskDtoBuilder dtos,
    ProjectAccess access)
    : ICommandHandler<SetTaskDependenciesCommand, TaskDto>
{
    public async Task<TaskDto> Handle(SetTaskDependenciesCommand command, CancellationToken ct)
    {
        // 1. Load it; the project must be open.
        var t = await tasks.GetAsync(command.Id, ct);
        await access.EnsureProjectAsync(t.ProjectId, ct);
        ProjectRules.EnsureOpen(await projects.GetAsync(t.ProjectId, ct));

        // 2. Only other tasks of the same project, and no loop.
        var wanted = command.DependsOn.Distinct().ToList();
        if (wanted.Contains(t.Id)) throw new FieldValidationException("dependsOn", "A task cannot depend on itself.");
        var projectTasks = await tasks.ListIdsOfProjectAsync(t.ProjectId, ct);
        if (wanted.Any(w => !projectTasks.Contains(w))) throw new FieldValidationException("dependsOn", "A task can only depend on tasks of the same project.");
        var edges = (await dependencies.ListForTasksAsync(projectTasks, ct))
            .Where(x => x.TaskId != t.Id).GroupBy(x => x.TaskId).ToDictionary(g => g.Key, g => g.Select(x => x.DependsOnTaskId).ToList());
        if (TaskGraph.WouldLoop(t.Id, wanted, edges)) throw new ConflictException("Those dependencies would make the tasks wait for each other in a loop.");

        // 3. Remove the ones no longer wanted, add the new ones and save.
        var existing = await dependencies.ListOfTaskForUpdateAsync(t.Id, ct);
        foreach (var d in existing.Where(d => !wanted.Contains(d.DependsOnTaskId))) dependencies.Remove(d);
        foreach (var w in wanted.Where(w => existing.All(d => d.DependsOnTaskId != w))) dependencies.Add(new ProjectTaskDependency { TaskId = t.Id, DependsOnTaskId = w });
        await dependencies.SaveChangesAsync(ct);
        return await dtos.BuildAsync(t, ct);
    }
}
