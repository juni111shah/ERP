using ERP.Application.Abstractions;
using ERP.Application.Reminders;
using ERP.Application.Search;
using ERP.Domain.Catalog;
using ERP.Modules.Inventory.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Inventory.Features;

/// <summary>Warehouses and stocked items for the global search.</summary>
public sealed class InventorySearch(IReadRepository<Warehouse> warehouses, IReadRepository<Item> items, IReadRepository<StockMovement> movements, ICurrentUser user) : ISearchProvider
{
    public string? ModuleKey => InventoryModule.Key;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string term, int limit, CancellationToken ct)
    {
        var hits = new List<SearchHit>();
        if (user.HasPermission(InventoryModule.WarehousesRead))
            hits.AddRange((await warehouses.Query(new Specification<Warehouse>(w => w.Name.Contains(term) || w.Code.Contains(term))).Take(limit).Select(w => new { w.Code, w.Name }).ToListAsync(ct))
                .Select(w => new SearchHit("Warehouses", w.Name, w.Code, "/inventory/warehouses")));
        if (user.HasPermission(InventoryModule.StockRead))
        {
            // Only items that have stock history, so the inventory results stay about inventory.
            var stocked = await movements.Query().Select(m => m.ItemId).Distinct().ToListAsync(ct);
            hits.AddRange((await items.Query(new Specification<Item>(i => stocked.Contains(i.Id) && (i.Sku.Contains(term) || i.Name.Contains(term)))).Take(limit).Select(i => new { i.Sku, i.Name }).ToListAsync(ct))
                .Select(i => new SearchHit("Stock items", i.Name, i.Sku, "/inventory/stock")));
        }
        return hits;
    }
}

/// <summary>Reminds stock keepers once a day per item while it stays at or below its reorder level. Reads the data directly: jobs run without a signed-in user.</summary>
public sealed class LowStockReminders(IReadRepository<StockPolicy> policies, IReadRepository<StockMovement> movements, IReadRepository<Item> items) : IReminderSource
{
    public string? ModuleKey => InventoryModule.Key;

    public async Task<IReadOnlyList<ReminderCandidate>> GetDueAsync(DateOnly today, CancellationToken ct)
    {
        var rules = await policies.Query().ToListAsync(ct);
        if (rules.Count == 0) return [];
        var ids = rules.Select(r => r.ItemId).ToList();
        var qty = (await movements.Query(new Specification<StockMovement>(m => ids.Contains(m.ItemId))).Select(m => new { m.ItemId, m.Quantity }).ToListAsync(ct)).GroupBy(m => m.ItemId).ToDictionary(g => g.Key, g => g.Sum(m => m.Quantity));
        var names = (await items.Query(new Specification<Item>(i => ids.Contains(i.Id))).Select(i => new { i.Id, i.Sku, i.Name }).ToListAsync(ct)).ToDictionary(i => i.Id);
        return rules.Where(r => names.ContainsKey(r.ItemId) && qty.GetValueOrDefault(r.ItemId) <= r.ReorderLevel).Select(r =>
        {
            var onHand = qty.GetValueOrDefault(r.ItemId); var i = names[r.ItemId];
            return new ReminderCandidate($"low-stock:{r.ItemId}:{today:yyyyMMdd}", $"Low stock: {i.Sku} {i.Name}",
                $"{onHand:0.####} left, reorder level {r.ReorderLevel:0.####}. Suggested order {Math.Max(r.ReorderQuantity, r.ReorderLevel - onHand):0.####}.", "/inventory/stock", InventoryModule.StockWrite);
        }).ToList();
    }
}
