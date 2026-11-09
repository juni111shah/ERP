using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Distribution.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Distribution.Repositories;

internal sealed class DistributionRouteRepository(IRepository<DistributionRoute> inner) : RepositoryBase<DistributionRoute>(inner), IDistributionRouteRepository
{
    public async Task<DistributionRoute> GetWithStopsForUpdateAsync(Guid id, CancellationToken ct)
        => await Tracked(new Specification<DistributionRoute>(r => r.Id == id))
            .Include(r => r.Stops)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Route {id} not found.");

    public Task<DistributionRoute?> FindWithStopsAsync(Guid id, CancellationToken ct)
        => Query(new Specification<DistributionRoute>(r => r.Id == id))
            .Include(r => r.Stops)
            .FirstOrDefaultAsync(ct);

    public Task<int> CountAllAsync(CancellationToken ct)
        => Query().CountAsync(ct);

    public Task<int> CountActiveAsync(CancellationToken ct)
        => Query(new Specification<DistributionRoute>(r => r.IsActive)).CountAsync(ct);
}
