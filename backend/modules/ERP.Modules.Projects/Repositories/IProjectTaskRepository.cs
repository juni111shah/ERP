using ERP.Application.Abstractions;
using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Repositories;

/// <summary>Data access for project tasks. Implemented in ProjectTaskRepository.cs.</summary>
public interface IProjectTaskRepository : IRepository<ProjectTask>
{
    /// <summary>How many tasks of the project are neither done nor cancelled.</summary>
    Task<int> CountOpenAsync(Guid projectId, CancellationToken ct);

    /// <summary>The ids of every task of the project.</summary>
    Task<List<Guid>> ListIdsOfProjectAsync(Guid projectId, CancellationToken ct);

    /// <summary>The numbers of those of the tasks that are neither done nor cancelled.</summary>
    Task<List<string>> ListOpenNumbersAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>How many subtasks of the task are neither done nor cancelled.</summary>
    Task<int> CountOpenSubtasksAsync(Guid parentId, CancellationToken ct);

    /// <summary>The task's id and parent when it belongs to the project; null otherwise.</summary>
    Task<TaskParentLink?> FindParentLinkAsync(Guid id, Guid projectId, CancellationToken ct);

    /// <summary>The title of a task; null when not found.</summary>
    Task<string?> GetTitleAsync(Guid id, CancellationToken ct);

    /// <summary>The titles of the tasks, by id.</summary>
    Task<Dictionary<Guid, string>> MapTitlesAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>"Number Title" of the tasks, by id.</summary>
    Task<Dictionary<Guid, string>> MapLabelsAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>The tasks of the project that are not cancelled, read-only.</summary>
    Task<List<ProjectTask>> ListLiveOfProjectAsync(Guid projectId, CancellationToken ct);

    /// <summary>Every task of the project, read-only.</summary>
    Task<List<ProjectTask>> ListOfProjectAsync(Guid projectId, CancellationToken ct);

    /// <summary>Every task of the project, tracked for changes.</summary>
    Task<List<ProjectTask>> ListOfProjectForUpdateAsync(Guid projectId, CancellationToken ct);

    /// <summary>Every task of the projects, read-only.</summary>
    Task<List<ProjectTask>> ListOfProjectsAsync(List<Guid> projectIds, CancellationToken ct);
}

/// <summary>A task and its parent.</summary>
public sealed record TaskParentLink(Guid Id, Guid? ParentTaskId);
