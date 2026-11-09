using ERP.Application.Abstractions;
using ERP.Modules.Projects.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Repositories;

internal sealed class ProjectAllocationRepository(IRepository<ProjectAllocation> inner) : RepositoryBase<ProjectAllocation>(inner), IProjectAllocationRepository
{
    public Task<List<ProjectAllocation>> ListByProjectAndUserAsync(Guid? projectId, Guid? userId, CancellationToken ct)
        => Query(new Specification<ProjectAllocation>(a => (projectId == null || a.ProjectId == projectId) && (userId == null || a.UserId == userId))).ToListAsync(ct);

    public Task<List<ProjectAllocation>> ListOverlappingAsync(DateOnly from, DateOnly to, CancellationToken ct)
        => Query(new Specification<ProjectAllocation>(a => a.FromDate <= to && a.ToDate >= from)).ToListAsync(ct);
}
