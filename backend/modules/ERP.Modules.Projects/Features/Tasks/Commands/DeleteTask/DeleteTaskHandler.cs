using ERP.Application.Common;
using ERP.Modules.Projects.Repositories;
using MediatR;

namespace ERP.Modules.Projects.Features;

internal sealed class DeleteTaskHandler(
    IProjectTaskRepository tasks,
    IProjectRepository projects,
    IProjectTimesheetRepository timesheets,
    IProjectTaskDependencyRepository dependencies,
    ProjectAccess access)
    : ICommandHandler<DeleteTaskCommand>
{
    public async Task<Unit> Handle(DeleteTaskCommand command, CancellationToken ct)
    {
        // 1. Load it; the project must be open.
        var t = await tasks.GetForUpdateAsync(command.Id, ct);
        await access.EnsureProjectAsync(t.ProjectId, ct);
        ProjectRules.EnsureOpen(await projects.GetAsync(t.ProjectId, ct));

        // 2. A task with time or subtasks stays.
        var id = t.Id;
        if (await timesheets.AnyAsync(x => x.TaskId == id, ct)) throw new ConflictException("Time has been logged against this task. Cancel the task instead of deleting it.");
        if (await tasks.AnyAsync(x => x.ParentTaskId == id, ct)) throw new ConflictException("This task has subtasks. Delete or move them first.");

        // 3. Remove it with every dependency it has or is the target of.
        foreach (var d in await dependencies.ListTouchingForUpdateAsync(id, ct)) dependencies.Remove(d);
        tasks.Remove(t);
        await tasks.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
