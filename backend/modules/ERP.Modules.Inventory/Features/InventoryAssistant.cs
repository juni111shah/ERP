using ERP.Application.Abstractions;
using ERP.Application.Assistant;
using ERP.Modules.Inventory.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Inventory.Features;

/// <summary>Stock on hand comes from the same query as the Stock on hand screen, so the assistant never works it out differently.</summary>
internal sealed class StockOnHandAssistantReader(ISender sender) : IAssistantReader
{
    public string Key => "stock_on_hand";
    public string Description => "Stock on hand by item and warehouse: SKU, item, warehouse, quantity and unit.";
    public string? ModuleKey => InventoryModule.Key;
    public string Permission => InventoryModule.StockRead;

    public async Task<object> ListAsync(string? search, int take, CancellationToken ct)
    {
        var stock = await sender.Send(new GetStockOnHand(null, null), ct);
        var rows = stock.Rows.AsEnumerable();
        if (search is not null) rows = rows.Where(r => r.Sku.Contains(search, StringComparison.OrdinalIgnoreCase) || r.Item.Contains(search, StringComparison.OrdinalIgnoreCase) || r.Warehouse.Contains(search, StringComparison.OrdinalIgnoreCase));
        return rows.Take(take).Select(r => new { r.Sku, r.Item, r.Warehouse, r.Quantity, r.Uom, Link = "/inventory/stock" }).ToList();
    }
}

internal sealed class StockCountsAssistantReader(IReadRepository<StockCount> counts, IReadRepository<Warehouse> warehouses) : IAssistantReader
{
    public string Key => "stock_counts";
    public string Description => "Physical stock counts, newest first: number, warehouse, date, status and number of items.";
    public string? ModuleKey => InventoryModule.Key;
    public string Permission => InventoryModule.CountsRead;

    public async Task<object> ListAsync(string? search, int take, CancellationToken ct)
    {
        var query = counts.Query();
        if (search is not null) query = query.Where(c => c.Number.Contains(search));
        return await query.OrderByDescending(c => c.CountDate).Take(take).Select(c => new { c.Number, Warehouse = warehouses.Query(null).Where(w => w.Id == c.WarehouseId).Select(w => w.Name).FirstOrDefault(), c.CountDate, Status = c.Status.ToString(), Items = c.Lines.Count, Link = "/inventory/counts/" + c.Id }).ToListAsync(ct);
    }
}

internal static class InventoryAssistantHelp
{
    public static readonly IReadOnlyList<AssistantHelpArticle> Articles =
    [
        new("Receive, issue, adjust or transfer stock", "Go to Inventory > Stock on hand and press Receive stock, Issue stock, Adjust or Transfer. Fill in the warehouse, item, quantity and date, then press Save.", "/inventory/stock", InventoryModule.StockRead),
        new("Set a reorder level", "On Inventory > Stock on hand press Reorder level, choose the item, enter the level and reorder quantity, then press Save. Items at or below the level are counted on the Low stock card.", "/inventory/stock", InventoryModule.StockRead),
        new("Count stock in a warehouse", "Go to Inventory > Stock counts, press New count, choose the warehouse and date and press Save. Open the count, enter what you found and press Save counts, then Post differences to book the adjustments.", "/inventory/counts", InventoryModule.CountsRead),
        new("See stock movements", "Inventory > Stock movements lists every receipt, issue, adjustment and transfer, with its date, item, warehouse and quantity.", "/inventory/movements", InventoryModule.StockRead),
    ];
}
