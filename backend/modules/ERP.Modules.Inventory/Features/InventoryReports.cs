using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Catalog;
using ERP.Modules.Inventory.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Inventory.Features;

public sealed record ValuationRow(string Warehouse, string Sku, string Item, string Uom, decimal Quantity, decimal AverageCost, decimal Value);
public sealed record ValuationDto(DateOnly AsOf, IReadOnlyList<ValuationRow> Rows, decimal TotalValue);
public sealed record MovementSummaryRow(string Sku, string Item, string Uom, decimal Opening, decimal Receipts, decimal Issues, decimal Adjustments, decimal Closing, decimal ClosingValue);
public sealed record MovementSummaryDto(DateOnly From, DateOnly To, IReadOnlyList<MovementSummaryRow> Rows, decimal TotalClosingValue);
public sealed record SlowMovingRow(string Sku, string Item, string Uom, decimal Quantity, decimal Value, DateOnly? LastMovement, int DaysIdle);
public sealed record SlowMovingDto(DateOnly AsOf, int Days, IReadOnlyList<SlowMovingRow> Rows, decimal TotalValue);
public sealed record ReorderRow(string Sku, string Item, string Uom, decimal OnHand, decimal ReorderLevel, decimal ReorderQuantity, decimal SuggestedOrder);
public sealed record ReorderDto(IReadOnlyList<ReorderRow> Rows);

/// <summary>Stock and value per warehouse and item at the end of a date (movements replayed up to it).</summary>
public sealed record GetStockValuation(DateOnly AsOf) : IQuery<ValuationDto>, IRequirePermission { public string RequiredPermission => InventoryModule.StockRead; }
/// <summary>Per item over a period: opening, receipts, issues, adjustments and closing quantity, with closing value. Transfers net to zero across warehouses and are left out.</summary>
public sealed record GetMovementSummary(DateOnly From, DateOnly To) : IQuery<MovementSummaryDto>, IRequirePermission { public string RequiredPermission => InventoryModule.StockRead; }
/// <summary>Items still in stock that have had no movement for the given number of days.</summary>
public sealed record GetSlowMoving(DateOnly AsOf, int Days) : IQuery<SlowMovingDto>, IRequirePermission { public string RequiredPermission => InventoryModule.StockRead; }
/// <summary>Items at or below their reorder level with a suggested order quantity (the larger of the reorder quantity and the shortfall to the level).</summary>
public sealed record GetReorderSuggestions : IQuery<ReorderDto>, IRequirePermission { public string RequiredPermission => InventoryModule.StockRead; }

internal sealed class InventoryReportHandlers(IReadRepository<StockMovement> movements, IReadRepository<StockPolicy> policies, IReadRepository<Warehouse> warehouses, IReadRepository<Item> items) :
    IRequestHandler<GetStockValuation, ValuationDto>,
    IRequestHandler<GetMovementSummary, MovementSummaryDto>,
    IRequestHandler<GetSlowMoving, SlowMovingDto>,
    IRequestHandler<GetReorderSuggestions, ReorderDto>
{
    private sealed record ItemInfo(string Sku, string Name, string Uom);

    public async Task<ValuationDto> Handle(GetStockValuation q, CancellationToken ct)
    {
        var all = await movements.Query(new Specification<StockMovement>(m => m.Date <= q.AsOf)).ToListAsync(ct);
        var (wMap, iMap) = await LookupsAsync(all, ct);
        var rows = all.GroupBy(m => (m.WarehouseId, m.ItemId)).Select(g => (g.Key, Pos: StockCosting.Replay(g))).Where(x => x.Pos.Quantity != 0)
            .Select(x => { var i = Info(iMap, x.Key.ItemId); return new ValuationRow(wMap.GetValueOrDefault(x.Key.WarehouseId, ""), i.Sku, i.Name, i.Uom, x.Pos.Quantity, x.Pos.AverageCost, x.Pos.Value); })
            .OrderBy(r => r.Sku).ThenBy(r => r.Warehouse).ToList();
        return new(q.AsOf, rows, rows.Sum(r => r.Value));
    }

    public async Task<MovementSummaryDto> Handle(GetMovementSummary q, CancellationToken ct)
    {
        var all = await movements.Query(new Specification<StockMovement>(m => m.Date <= q.To)).ToListAsync(ct);
        var (_, iMap) = await LookupsAsync(all, ct);
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
            var i = Info(iMap, byItem.Key);
            rows.Add(new(i.Sku, i.Name, i.Uom, before, receipts, issues, adjustments, closing, closingValue));
        }
        rows = rows.OrderBy(r => r.Sku).ToList();
        return new(q.From, q.To, rows, rows.Sum(r => r.ClosingValue));
    }

    public async Task<SlowMovingDto> Handle(GetSlowMoving q, CancellationToken ct)
    {
        var all = await movements.Query(new Specification<StockMovement>(m => m.Date <= q.AsOf)).ToListAsync(ct);
        var (_, iMap) = await LookupsAsync(all, ct);
        var rows = new List<SlowMovingRow>();
        foreach (var byItem in all.GroupBy(m => m.ItemId))
        {
            var qty = byItem.Sum(m => m.Quantity);
            if (qty <= 0) continue;
            var last = byItem.Max(m => m.Date);
            var idle = q.AsOf.DayNumber - last.DayNumber;
            if (idle < q.Days) continue;
            var i = Info(iMap, byItem.Key);
            rows.Add(new(i.Sku, i.Name, i.Uom, qty, byItem.GroupBy(m => m.WarehouseId).Sum(g => StockCosting.Replay(g).Value), last, idle));
        }
        rows = rows.OrderByDescending(r => r.DaysIdle).ThenBy(r => r.Sku).ToList();
        return new(q.AsOf, q.Days, rows, rows.Sum(r => r.Value));
    }

    public async Task<ReorderDto> Handle(GetReorderSuggestions q, CancellationToken ct)
    {
        var rules = await policies.Query().ToListAsync(ct);
        if (rules.Count == 0) return new([]);
        var ids = rules.Select(r => r.ItemId).ToList();
        var qty = (await movements.Query(new Specification<StockMovement>(m => ids.Contains(m.ItemId))).Select(m => new { m.ItemId, m.Quantity }).ToListAsync(ct)).GroupBy(m => m.ItemId).ToDictionary(g => g.Key, g => g.Sum(m => m.Quantity));
        var byId = (await items.Query(new Specification<Item>(i => ids.Contains(i.Id))).Select(i => new { i.Id, i.Sku, i.Name, i.Uom }).ToListAsync(ct)).ToDictionary(i => i.Id);
        var rows = rules.Where(r => qty.GetValueOrDefault(r.ItemId) <= r.ReorderLevel).Select(r =>
        {
            var onHand = qty.GetValueOrDefault(r.ItemId);
            var i = byId.GetValueOrDefault(r.ItemId);
            return new ReorderRow(i?.Sku ?? "?", i?.Name ?? "(deleted item)", i?.Uom ?? "", onHand, r.ReorderLevel, r.ReorderQuantity, Math.Max(r.ReorderQuantity, r.ReorderLevel - onHand));
        }).OrderBy(r => r.Sku).ToList();
        return new(rows);
    }

    private async Task<(Dictionary<Guid, string> Warehouses, Dictionary<Guid, ItemInfo> Items)> LookupsAsync(List<StockMovement> all, CancellationToken ct)
    {
        var wIds = all.Select(m => m.WarehouseId).Distinct().ToList(); var iIds = all.Select(m => m.ItemId).Distinct().ToList();
        var w = await warehouses.Query(new Specification<Warehouse>(x => wIds.Contains(x.Id))).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var i = (await items.Query(new Specification<Item>(x => iIds.Contains(x.Id))).Select(x => new { x.Id, x.Sku, x.Name, x.Uom }).ToListAsync(ct)).ToDictionary(x => x.Id, x => new ItemInfo(x.Sku, x.Name, x.Uom));
        return (w, i);
    }

    private static ItemInfo Info(Dictionary<Guid, ItemInfo> map, Guid id) => map.GetValueOrDefault(id) ?? new("?", "(deleted item)", "");
}
