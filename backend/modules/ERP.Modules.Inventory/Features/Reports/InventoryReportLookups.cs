using ERP.Modules.Inventory.Domain;
using ERP.Modules.Inventory.Repositories;

namespace ERP.Modules.Inventory.Features;

/// <summary>The warehouse names and item details the stock reports show next to replayed movements.</summary>
internal sealed class InventoryReportLookups(IWarehouseRepository warehouses, IStockItemRepository items)
{
    public sealed record ItemInfo(string Sku, string Name, string Uom);

    public async Task<(Dictionary<Guid, string> Warehouses, Dictionary<Guid, ItemInfo> Items)> LookupsAsync(List<StockMovement> all, CancellationToken ct)
    {
        var wIds = all.Select(m => m.WarehouseId).Distinct().ToList(); var iIds = all.Select(m => m.ItemId).Distinct().ToList();
        var w = await warehouses.MapNamesAsync(wIds, ct);
        var i = (await items.MapInfoAsync(iIds, ct)).Values.ToDictionary(x => x.Id, x => new ItemInfo(x.Sku, x.Name, x.Uom));
        return (w, i);
    }

    public static ItemInfo Info(Dictionary<Guid, ItemInfo> map, Guid id) => map.GetValueOrDefault(id) ?? new("?", "(deleted item)", "");
}
