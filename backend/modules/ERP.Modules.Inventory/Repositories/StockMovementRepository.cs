using ERP.Application.Abstractions;
using ERP.Modules.Inventory.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Inventory.Repositories;

internal sealed class StockMovementRepository(IRepository<StockMovement> inner) : RepositoryBase<StockMovement>(inner), IStockMovementRepository
{
    public Task<List<StockMovement>> ListByWarehouseAndItemAsync(Guid? warehouseId, Guid? itemId, CancellationToken ct)
        => Query(new Specification<StockMovement>(m => (warehouseId == null || m.WarehouseId == warehouseId) && (itemId == null || m.ItemId == itemId))).ToListAsync(ct);

    public Task<List<StockMovement>> ListForItemInWarehouseAsync(Guid warehouseId, Guid itemId, CancellationToken ct)
        => Query(new Specification<StockMovement>(m => m.WarehouseId == warehouseId && m.ItemId == itemId)).ToListAsync(ct);

    public Task<List<StockMovement>> ListForItemInWarehouseUpToAsync(Guid warehouseId, Guid itemId, DateOnly? asOf, CancellationToken ct)
        => Query(new Specification<StockMovement>(m => m.WarehouseId == warehouseId && m.ItemId == itemId && (asOf == null || m.Date <= asOf))).ToListAsync(ct);

    public Task<List<StockMovement>> ListForItemsInWarehouseAsync(Guid warehouseId, List<Guid> itemIds, CancellationToken ct)
        => Query(new Specification<StockMovement>(m => m.WarehouseId == warehouseId && itemIds.Contains(m.ItemId))).ToListAsync(ct);

    public Task<List<StockMovement>> ListForWarehouseAsync(Guid warehouseId, CancellationToken ct)
        => Query(new Specification<StockMovement>(m => m.WarehouseId == warehouseId)).ToListAsync(ct);

    public Task<List<StockMovement>> ListUpToAsync(DateOnly asOf, CancellationToken ct)
        => Query(new Specification<StockMovement>(m => m.Date <= asOf)).ToListAsync(ct);

    public Task<List<StockMovement>> ListUpToForLegalEntityAsync(DateOnly asOf, Guid? legalEntityId, CancellationToken ct)
        => Query(new Specification<StockMovement>(m => m.Date <= asOf && (legalEntityId == null || m.LegalEntityId == legalEntityId))).ToListAsync(ct);

    public async Task<Dictionary<Guid, decimal>> QuantityByItemAsync(List<Guid> itemIds, CancellationToken ct)
        => (await Query(new Specification<StockMovement>(m => itemIds.Contains(m.ItemId))).Select(m => new { m.ItemId, m.Quantity }).ToListAsync(ct))
            .GroupBy(m => m.ItemId).ToDictionary(g => g.Key, g => g.Sum(m => m.Quantity));
}
