using ERP.Modules.Inventory.Domain;
using ERP.Modules.Inventory.Repositories;

namespace ERP.Modules.Inventory.Features;

/// <summary>Shapes a stock count for the screens: its warehouse name and each line with the item's SKU, name and unit, by SKU.</summary>
internal sealed class StockCountDtoBuilder(IWarehouseRepository warehouses, IStockItemRepository items)
{
    public async Task<StockCountDto> ToDtoAsync(StockCount c, CancellationToken ct)
    {
        var wh = await warehouses.GetAsync(c.WarehouseId, ct);
        var ids = c.Lines.Select(l => l.ItemId).ToList();
        var map = await items.MapInfoAsync(ids, ct);
        var lines = c.Lines.Select(l => new { l, i = map.GetValueOrDefault(l.ItemId) }).OrderBy(x => x.i?.Sku)
            .Select(x => new StockCountLineDto(x.l.ItemId, x.i?.Sku ?? "?", x.i?.Name ?? "(deleted item)", x.i?.Uom ?? "", x.l.SystemQuantity, x.l.CountedQuantity, x.l.CountedQuantity is { } q ? q - x.l.SystemQuantity : null, x.l.MovementId)).ToList();
        return new(c.Id, c.Number, c.WarehouseId, wh.Name, c.CountDate, c.Status, c.Memo, c.PostedAt, lines);
    }
}
