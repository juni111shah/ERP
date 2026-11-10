using ERP.Application.Common;
using ERP.Modules.Inventory.Repositories;

namespace ERP.Modules.Inventory.Features;

internal sealed class GetStockValuationHandler(IStockMovementRepository movements, InventoryReportLookups lookups) : IQueryHandler<GetStockValuationQuery, ValuationDto>
{
    public async Task<ValuationDto> Handle(GetStockValuationQuery q, CancellationToken ct)
    {
        // 1. Every movement up to the date, with the warehouse and item names.
        var all = await movements.ListUpToAsync(q.AsOf, ct);
        var (wMap, iMap) = await lookups.LookupsAsync(all, ct);

        // 2. Replay each warehouse/item; keep what is still held.
        var rows = all.GroupBy(m => (m.WarehouseId, m.ItemId)).Select(g => (g.Key, Pos: StockCosting.Replay(g))).Where(x => x.Pos.Quantity != 0)
            .Select(x => { var i = InventoryReportLookups.Info(iMap, x.Key.ItemId); return new ValuationRow(wMap.GetValueOrDefault(x.Key.WarehouseId, ""), i.Sku, i.Name, i.Uom, x.Pos.Quantity, x.Pos.AverageCost, x.Pos.Value); })
            .OrderBy(r => r.Sku).ThenBy(r => r.Warehouse).ToList();
        return new(q.AsOf, rows, rows.Sum(r => r.Value));
    }
}
