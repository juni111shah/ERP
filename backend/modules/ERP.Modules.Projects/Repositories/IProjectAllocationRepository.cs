using ERP.Application.Abstractions;
using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Repositories;

/// <summary>Data access for resource allocations. Implemented in ProjectAllocationRepository.cs.</summary>
public interface IProjectAllocationRepository : IRepository<ProjectAllocation>
{
    /// <summary>The allocations of a project and/or a person (either may be null for all), read-only.</summary>
    Task<List<ProjectAllocation>> ListByProjectAndUserAsync(Guid? projectId, Guid? userId, CancellationToken ct);

    /// <summary>The allocations that overlap the period, read-only.</summary>
    Task<List<ProjectAllocation>> ListOverlappingAsync(DateOnly from, DateOnly to, CancellationToken ct);
}
