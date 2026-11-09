using ERP.Application.Abstractions;
using ERP.Modules.Distribution.Domain;

namespace ERP.Modules.Distribution.Repositories;

/// <summary>Data access for delivery routes. Implemented in DistributionRouteRepository.cs.</summary>
public interface IDistributionRouteRepository : IRepository<DistributionRoute>
{
    /// <summary>The route with its stops, tracked for changes. Throws NotFoundException.</summary>
    Task<DistributionRoute> GetWithStopsForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>The route with its stops, read-only; null when it does not exist.</summary>
    Task<DistributionRoute?> FindWithStopsAsync(Guid id, CancellationToken ct);

    /// <summary>The number of routes.</summary>
    Task<int> CountAllAsync(CancellationToken ct);

    /// <summary>The number of active routes.</summary>
    Task<int> CountActiveAsync(CancellationToken ct);
}
