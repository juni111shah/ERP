using ERP.Application.Abstractions;
using ERP.Modules.Inventory.Domain;

namespace ERP.Modules.Inventory.Repositories;

/// <summary>Data access for reorder rules.</summary>
public interface IStockPolicyRepository : IRepository<StockPolicy>
{
    /// <summary>The reorder rule of an item, tracked for changes, or null when it has none.</summary>
    Task<StockPolicy?> FindForItemForUpdateAsync(Guid itemId, CancellationToken ct);
}
