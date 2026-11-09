using ERP.Application.Abstractions;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Features;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Repositories;

internal sealed class ProjectProgressBillingRepository(IRepository<ProjectProgressBilling> inner) : RepositoryBase<ProjectProgressBilling>(inner), IProjectProgressBillingRepository
{
    public Task<decimal> SumActiveAsync(Guid projectId, CancellationToken ct)
        => Query(new Specification<ProjectProgressBilling>(x => x.ProjectId == projectId && !x.Released)).SumDecimalAsync(x => x.Amount, ct);

    public Task<List<ProjectProgressBilling>> ListActiveAsync(CancellationToken ct)
        => Query(new Specification<ProjectProgressBilling>(x => !x.Released)).ToListAsync(ct);

    public Task<List<ProjectProgressBilling>> ListActiveOfProjectsAsync(List<Guid> projectIds, CancellationToken ct)
        => Query(new Specification<ProjectProgressBilling>(x => projectIds.Contains(x.ProjectId) && !x.Released)).ToListAsync(ct);

    public Task<List<ProjectProgressBilling>> ListActiveOfProjectsBetweenAsync(List<Guid> projectIds, DateOnly? from, DateOnly? to, CancellationToken ct)
        => Query(new Specification<ProjectProgressBilling>(x => projectIds.Contains(x.ProjectId) && !x.Released && (from == null || x.Date >= from) && (to == null || x.Date <= to))).ToListAsync(ct);
}
