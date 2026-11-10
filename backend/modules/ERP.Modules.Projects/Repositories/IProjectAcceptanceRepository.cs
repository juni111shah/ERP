using ERP.Application.Abstractions;
using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Repositories;

/// <summary>Data access for customer sign-offs. Implemented in ProjectAcceptanceRepository.cs.</summary>
public interface IProjectAcceptanceRepository : IRepository<ProjectAcceptance>
{
    /// <summary>The sign-offs of one project, or (null) of all of them, read-only.</summary>
    Task<List<ProjectAcceptance>> ListByProjectAsync(Guid? projectId, CancellationToken ct);

    /// <summary>Every sign-off of the project, read-only.</summary>
    Task<List<ProjectAcceptance>> ListOfProjectAsync(Guid projectId, CancellationToken ct);

    /// <summary>Every sign-off of the projects, read-only.</summary>
    Task<List<ProjectAcceptance>> ListOfProjectsAsync(List<Guid> projectIds, CancellationToken ct);
}
