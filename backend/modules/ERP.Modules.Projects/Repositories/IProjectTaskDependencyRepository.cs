using ERP.Application.Abstractions;
using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Repositories;

/// <summary>Data access for task dependencies. Implemented in ProjectTaskDependencyRepository.cs.</summary>
public interface IProjectTaskDependencyRepository : IRepository<ProjectTaskDependency>
{
    /// <summary>The dependencies the task has or is the target of, tracked for changes.</summary>
    Task<List<ProjectTaskDependency>> ListTouchingForUpdateAsync(Guid taskId, CancellationToken ct);

    /// <summary>The dependencies of the tasks, read-only.</summary>
    Task<List<ProjectTaskDependency>> ListForTasksAsync(List<Guid> taskIds, CancellationToken ct);

    /// <summary>The dependencies of one task, tracked for changes.</summary>
    Task<List<ProjectTaskDependency>> ListOfTaskForUpdateAsync(Guid taskId, CancellationToken ct);

    /// <summary>The tasks the task waits for.</summary>
    Task<List<Guid>> ListDependsOnAsync(Guid taskId, CancellationToken ct);
}
