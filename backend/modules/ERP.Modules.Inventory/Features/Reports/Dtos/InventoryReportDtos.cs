namespace ERP.Modules.Inventory.Features;

public sealed record ValuationRow(string Warehouse, string Sku, string Item, string Uom, decimal Quantity, decimal AverageCost, decimal Value);
public sealed record ValuationDto(DateOnly AsOf, IReadOnlyList<ValuationRow> Rows, decimal TotalValue);
public sealed record MovementSummaryRow(string Sku, string Item, string Uom, decimal Opening, decimal Receipts, decimal Issues, decimal Adjustments, decimal Closing, decimal ClosingValue);
public sealed record MovementSummaryDto(DateOnly From, DateOnly To, IReadOnlyList<MovementSummaryRow> Rows, decimal TotalClosingValue);
public sealed record SlowMovingRow(string Sku, string Item, string Uom, decimal Quantity, decimal Value, DateOnly? LastMovement, int DaysIdle);
public sealed record SlowMovingDto(DateOnly AsOf, int Days, IReadOnlyList<SlowMovingRow> Rows, decimal TotalValue);
public sealed record ReorderRow(string Sku, string Item, string Uom, decimal OnHand, decimal ReorderLevel, decimal ReorderQuantity, decimal SuggestedOrder);
public sealed record ReorderDto(IReadOnlyList<ReorderRow> Rows);
public sealed record StockGlReconciliationRow(Guid LegalEntityId, string LegalEntity, string CurrencyCode, decimal StockValue, decimal InventoryAccountBalance, decimal Difference);
public sealed record StockGlReconciliationDto(DateOnly AsOf, IReadOnlyList<StockGlReconciliationRow> Rows, bool Reconciled);
