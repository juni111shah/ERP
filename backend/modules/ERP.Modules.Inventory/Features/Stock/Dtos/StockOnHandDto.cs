namespace ERP.Modules.Inventory.Features;

public sealed record StockRow(Guid WarehouseId, string Warehouse, Guid ItemId, string Sku, string Item, string Uom, decimal Quantity, decimal AverageCost, decimal Value);
public sealed record StockOnHandDto(IReadOnlyList<StockRow> Rows, decimal TotalValue);
public sealed record LowStockRow(Guid ItemId, string Sku, string Item, decimal OnHand, decimal ReorderLevel, decimal ReorderQuantity);
