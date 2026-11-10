using ERP.Application.Common;
using ERP.Modules.Inventory.Repositories;

namespace ERP.Modules.Inventory.Features;

internal sealed class GetSlowMovingHandler(IStockMovementRepository movements, InventoryReportLookups lookups) : IQueryHandler<GetSlowMovingQuery, SlowMovingDto>
{
    public async Task<SlowMovingDto> Handle(GetSlowMovingQuery q, CancellationToken ct)
    {
        // 1. Every movement up to the date, with the item names.
        var all = await movements.ListUpToAsync(q.AsOf, ct);
        var (_, iMap) = await lookups.LookupsAsync(all, ct);

        // 2. The items still held whose last movement is at least the given days back.
        var rows = new List<SlowMovingRow>();
        foreach (var byItem in all.GroupBy(m => m.ItemId))
        {
            var qty = byItem.Sum(m => m.Quantity);
            if (qty <= 0) continue;
            var last = byItem.Max(m => m.Date);
            var idle = q.AsOf.DayNumber - last.DayNumber;
            if (idle < q.Days) continue;
            var i = InventoryReportLookups.Info(iMap, byItem.Key);
            rows.Add(new(i.Sku, i.Name, i.Uom, qty, byItem.GroupBy(m => m.WarehouseId).Sum(g => StockCosting.Replay(g).Value), last, idle));
        }
        rows = rows.OrderByDescending(r => r.DaysIdle).ThenBy(r => r.Sku).ToList();
        return new(q.AsOf, q.Days, rows, rows.Sum(r => r.Value));
    }
}
