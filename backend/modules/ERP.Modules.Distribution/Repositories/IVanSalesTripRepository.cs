using ERP.Application.Abstractions;
using ERP.Modules.Distribution.Domain;

namespace ERP.Modules.Distribution.Repositories;

/// <summary>Data access for van sales trips. Implemented in VanSalesTripRepository.cs.</summary>
public interface IVanSalesTripRepository : IRepository<VanSalesTrip>
{
    /// <summary>The trip with its loaded items, tracked for changes. Throws NotFoundException.</summary>
    Task<VanSalesTrip> GetWithItemsForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>The trip with its loaded items, read-only; null when it does not exist.</summary>
    Task<VanSalesTrip?> FindWithItemsAsync(Guid id, CancellationToken ct);

    /// <summary>The number of trips in progress that departed on <paramref name="date"/>.</summary>
    Task<int> CountInProgressDepartedOnAsync(DateOnly date, CancellationToken ct);

    /// <summary>The completed trips that departed on or after <paramref name="from"/>.</summary>
    Task<List<VanSalesTrip>> ListCompletedDepartedSinceAsync(DateOnly from, CancellationToken ct);
}
