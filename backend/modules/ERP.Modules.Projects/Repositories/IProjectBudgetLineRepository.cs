using ERP.Application.Abstractions;
using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Repositories;

/// <summary>Data access for budget lines. Implemented in ProjectBudgetLineRepository.cs.</summary>
public interface IProjectBudgetLineRepository : IRepository<ProjectBudgetLine>
{
    /// <summary>Every budget line of the project, read-only.</summary>
    Task<List<ProjectBudgetLine>> ListOfProjectAsync(Guid projectId, CancellationToken ct);
}
