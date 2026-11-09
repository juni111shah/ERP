using ERP.Application.Common;
using ERP.Modules.Inventory.Domain;
using ERP.Modules.Inventory.Repositories;

namespace ERP.Modules.Inventory.Features;

internal sealed class GetMovementSummaryHandler(IStockMovementRepository movements, InventoryReportLookups lookups) : IQueryHandler<GetMovementSummaryQuery, MovementSummaryDto>
{
    public async Task<MovementSummaryDto> Handle(GetMovementSummaryQuery q, CancellationToken ct)
    {
        // 1. Every movement up to the end of the period, with the item names.
        var all = await movements.ListUpToAsync(q.To, ct);
        var (_, iMap) = await lookups.LookupsAsync(all, ct);

        // 2. Per item: what was there before the period, what moved in it, and what is left.
        var rows = new List<MovementSummaryRow>();
        foreach (var byItem in all.GroupBy(m => m.ItemId))
        {
            var before = byItem.Where(m => m.Date < q.From).Sum(m => m.Quantity);
            var inPeriod = byItem.Where(m => m.Date >= q.From).ToList();
            var receipts = inPeriod.Where(m => m.Kind == MovementKind.Receipt).Sum(m => m.Quantity);
            var issues = -inPeriod.Where(m => m.Kind == MovementKind.Issue).Sum(m => m.Quantity);
            var adjustments = inPeriod.Where(m => m.Kind == MovementKind.Adjustment).Sum(m => m.Quantity);
            var closing = before + inPeriod.Sum(m => m.Quantity);
            var closingValue = byItem.GroupBy(m => m.WarehouseId).Sum(g => StockCosting.Replay(g).Value);
            if (before == 0 && inPeriod.Count == 0 && closing == 0) continue;
            var i = InventoryReportLookups.Info(iMap, byItem.Key);
            rows.Add(new(i.Sku, i.Name, i.Uom, before, receipts, issues, adjustments, closing, closingValue));
        }
        rows = rows.OrderBy(r => r.Sku).ToList();
        return new(q.From, q.To, rows, rows.Sum(r => r.ClosingValue));
    }
}
