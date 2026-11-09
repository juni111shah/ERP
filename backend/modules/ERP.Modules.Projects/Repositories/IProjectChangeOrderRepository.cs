using ERP.Application.Abstractions;
using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Repositories;

/// <summary>Data access for change orders. Implemented in ProjectChangeOrderRepository.cs.</summary>
public interface IProjectChangeOrderRepository : IRepository<ProjectChangeOrder>
{
    /// <summary>Every change order of the project, read-only.</summary>
    Task<List<ProjectChangeOrder>> ListForProjectAsync(Guid projectId, CancellationToken ct);

    /// <summary>Every change order of the projects, read-only.</summary>
    Task<List<ProjectChangeOrder>> ListForProjectsAsync(List<Guid> projectIds, CancellationToken ct);

    /// <summary>The change orders of one project, or (null) of all of them, read-only.</summary>
    Task<List<ProjectChangeOrder>> ListByProjectAsync(Guid? projectId, CancellationToken ct);

    /// <summary>Every approved change order, read-only.</summary>
    Task<List<ProjectChangeOrder>> ListApprovedAsync(CancellationToken ct);

    /// <summary>The numbers given so far to the project's change orders.</summary>
    Task<List<string>> ListNumbersOfProjectAsync(Guid projectId, CancellationToken ct);
}
