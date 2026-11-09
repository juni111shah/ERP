using ERP.Application.Abstractions;
using ERP.Modules.Projects.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Repositories;

internal sealed class ProjectChangeOrderRepository(IRepository<ProjectChangeOrder> inner) : RepositoryBase<ProjectChangeOrder>(inner), IProjectChangeOrderRepository
{
    public Task<List<ProjectChangeOrder>> ListForProjectAsync(Guid projectId, CancellationToken ct)
        => Query(new Specification<ProjectChangeOrder>(c => c.ProjectId == projectId)).ToListAsync(ct);

    public Task<List<ProjectChangeOrder>> ListForProjectsAsync(List<Guid> projectIds, CancellationToken ct)
        => Query(new Specification<ProjectChangeOrder>(c => projectIds.Contains(c.ProjectId))).ToListAsync(ct);

    public Task<List<ProjectChangeOrder>> ListByProjectAsync(Guid? projectId, CancellationToken ct)
        => Query(new Specification<ProjectChangeOrder>(c => projectId == null || c.ProjectId == projectId)).ToListAsync(ct);

    public Task<List<ProjectChangeOrder>> ListApprovedAsync(CancellationToken ct)
        => Query(new Specification<ProjectChangeOrder>(x => x.Status == ChangeOrderStatus.Approved)).ToListAsync(ct);

    public Task<List<string>> ListNumbersOfProjectAsync(Guid projectId, CancellationToken ct)
        => Query(new Specification<ProjectChangeOrder>(c => c.ProjectId == projectId)).Select(c => c.Number).ToListAsync(ct);
}
