using ERP.Application.Abstractions;
using ERP.Modules.Projects.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Repositories;

internal sealed class ProjectTaskDependencyRepository(IRepository<ProjectTaskDependency> inner) : RepositoryBase<ProjectTaskDependency>(inner), IProjectTaskDependencyRepository
{
    public Task<List<ProjectTaskDependency>> ListTouchingForUpdateAsync(Guid taskId, CancellationToken ct)
        => Tracked(new Specification<ProjectTaskDependency>(x => x.TaskId == taskId || x.DependsOnTaskId == taskId)).ToListAsync(ct);

    public Task<List<ProjectTaskDependency>> ListForTasksAsync(List<Guid> taskIds, CancellationToken ct)
        => Query(new Specification<ProjectTaskDependency>(x => taskIds.Contains(x.TaskId))).ToListAsync(ct);

    public Task<List<ProjectTaskDependency>> ListOfTaskForUpdateAsync(Guid taskId, CancellationToken ct)
        => Tracked(new Specification<ProjectTaskDependency>(x => x.TaskId == taskId)).ToListAsync(ct);

    public Task<List<Guid>> ListDependsOnAsync(Guid taskId, CancellationToken ct)
        => Query(new Specification<ProjectTaskDependency>(d => d.TaskId == taskId)).Select(d => d.DependsOnTaskId).ToListAsync(ct);
}
