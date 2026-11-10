using ERP.Application.Common;
using ERP.Modules.Inventory.Repositories;

namespace ERP.Modules.Inventory.Features;

internal sealed class GetStockOnHandHandler(IStockMovementRepository movements, IWarehouseRepository warehouses, IStockItemRepository items) : IQueryHandler<GetStockOnHandQuery, StockOnHandDto>
{
    public async Task<StockOnHandDto> Handle(GetStockOnHandQuery q, CancellationToken ct)
    {
        // 1. The movements in scope, and the names of their warehouses and items.
        var all = await movements.ListByWarehouseAndItemAsync(q.WarehouseId, q.ItemId, ct);
        var wIds = all.Select(m => m.WarehouseId).Distinct().ToList(); var iIds = all.Select(m => m.ItemId).Distinct().ToList();
        var wMap = await warehouses.MapNamesAsync(wIds, ct);
        var iMap = await items.MapInfoAsync(iIds, ct);

        // 2. Replay each warehouse/item to its position; keep what is still held.
        var rows = all.GroupBy(m => (m.WarehouseId, m.ItemId)).Select(g => (g.Key, Pos: StockCosting.Replay(g))).Where(x => x.Pos.Quantity != 0)
            .Select(x => new StockRow(x.Key.WarehouseId, wMap.GetValueOrDefault(x.Key.WarehouseId, ""), x.Key.ItemId, iMap.GetValueOrDefault(x.Key.ItemId)?.Sku ?? "?", iMap.GetValueOrDefault(x.Key.ItemId)?.Name ?? "(deleted item)", iMap.GetValueOrDefault(x.Key.ItemId)?.Uom ?? "", x.Pos.Quantity, x.Pos.AverageCost, x.Pos.Value))
            .OrderBy(r => r.Sku).ThenBy(r => r.Warehouse).ToList();
        return new(rows, rows.Sum(r => r.Value));
    }
}
