using ERP.Application.Abstractions;
using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Repositories;

/// <summary>Data access for fixed-fee progress billings. Implemented in ProjectProgressBillingRepository.cs.</summary>
public interface IProjectProgressBillingRepository : IRepository<ProjectProgressBilling>
{
    /// <summary>The amount of the project's progress billings that were not released by a void.</summary>
    Task<decimal> SumActiveAsync(Guid projectId, CancellationToken ct);

    /// <summary>Every progress billing that was not released by a void, read-only.</summary>
    Task<List<ProjectProgressBilling>> ListActiveAsync(CancellationToken ct);

    /// <summary>The progress billings of the projects that were not released, read-only.</summary>
    Task<List<ProjectProgressBilling>> ListActiveOfProjectsAsync(List<Guid> projectIds, CancellationToken ct);

    /// <summary>The progress billings of the projects that were not released, dated in the (open-ended) period, read-only.</summary>
    Task<List<ProjectProgressBilling>> ListActiveOfProjectsBetweenAsync(List<Guid> projectIds, DateOnly? from, DateOnly? to, CancellationToken ct);
}
