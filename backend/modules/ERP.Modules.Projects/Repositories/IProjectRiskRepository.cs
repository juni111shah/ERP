using ERP.Application.Abstractions;
using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Repositories;

/// <summary>Data access for risks and issues. Implemented in ProjectRiskRepository.cs.</summary>
public interface IProjectRiskRepository : IRepository<ProjectRisk>
{
    /// <summary>The risks of one project, or (null) of all of them, read-only.</summary>
    Task<List<ProjectRisk>> ListByProjectAsync(Guid? projectId, CancellationToken ct);

    /// <summary>Every risk of the project, read-only.</summary>
    Task<List<ProjectRisk>> ListOfProjectAsync(Guid projectId, CancellationToken ct);
}
