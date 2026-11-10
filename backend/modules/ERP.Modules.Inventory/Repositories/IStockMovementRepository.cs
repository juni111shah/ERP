using ERP.Application.Abstractions;
using ERP.Modules.Inventory.Domain;

namespace ERP.Modules.Inventory.Repositories;

/// <summary>Data access for the stock ledger (stock movements).</summary>
public interface IStockMovementRepository : IRepository<StockMovement>
{
    /// <summary>The movements of one warehouse and/or one item (every one when both are null).</summary>
    Task<List<StockMovement>> ListByWarehouseAndItemAsync(Guid? warehouseId, Guid? itemId, CancellationToken ct);

    /// <summary>The movements of one item in one warehouse.</summary>
    Task<List<StockMovement>> ListForItemInWarehouseAsync(Guid warehouseId, Guid itemId, CancellationToken ct);

    /// <summary>The movements of one item in one warehouse, only up to <paramref name="asOf"/> when it is set.</summary>
    Task<List<StockMovement>> ListForItemInWarehouseUpToAsync(Guid warehouseId, Guid itemId, DateOnly? asOf, CancellationToken ct);

    /// <summary>The movements of the given items in one warehouse.</summary>
    Task<List<StockMovement>> ListForItemsInWarehouseAsync(Guid warehouseId, List<Guid> itemIds, CancellationToken ct);

    /// <summary>Every movement of one warehouse.</summary>
    Task<List<StockMovement>> ListForWarehouseAsync(Guid warehouseId, CancellationToken ct);

    /// <summary>Every movement dated on or before <paramref name="asOf"/>.</summary>
    Task<List<StockMovement>> ListUpToAsync(DateOnly asOf, CancellationToken ct);

    /// <summary>Every movement dated on or before <paramref name="asOf"/>, of one legal entity or (null) all of them.</summary>
    Task<List<StockMovement>> ListUpToForLegalEntityAsync(DateOnly asOf, Guid? legalEntityId, CancellationToken ct);

    /// <summary>Quantity on hand (all warehouses) of each of the items that has movements, by item id.</summary>
    Task<Dictionary<Guid, decimal>> QuantityByItemAsync(List<Guid> itemIds, CancellationToken ct);
}
