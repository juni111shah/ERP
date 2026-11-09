using ERP.Application.Abstractions;
using ERP.Modules.Inventory.Domain;

namespace ERP.Modules.Inventory.Repositories;

/// <summary>Data access for physical stock counts.</summary>
public interface IStockCountRepository : IRepository<StockCount>
{
    /// <summary>The count with its lines, read-only. Throws NotFoundException.</summary>
    Task<StockCount> GetWithLinesAsync(Guid id, CancellationToken ct);

    /// <summary>The count with its lines, tracked for changes. Throws NotFoundException.</summary>
    Task<StockCount> GetWithLinesForUpdateAsync(Guid id, CancellationToken ct);
}
