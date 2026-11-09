using ERP.Application.Abstractions;
using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Repositories;

/// <summary>Data access for rate cards. Implemented in ProjectRateRepository.cs.</summary>
public interface IProjectRateRepository : IRepository<ProjectRate>
{
    /// <summary>The rates set on the project itself, read-only.</summary>
    Task<List<ProjectRate>> ListForProjectAsync(Guid projectId, CancellationToken ct);

    /// <summary>The rates that apply to a project (its own and the workspace-wide ones), or every rate when <paramref name="projectId"/> is null.</summary>
    Task<List<ProjectRate>> ListApplicableAsync(Guid? projectId, CancellationToken ct);

    /// <summary>The rates of exactly this project (or workspace-wide) and person (or nobody), read-only.</summary>
    Task<List<ProjectRate>> ListForProjectAndUserAsync(Guid? projectId, Guid? userId, CancellationToken ct);
}
