namespace ERP.Modules.Inventory.Features;

public sealed record ReceiveStockInput(Guid WarehouseId, Guid ItemId, decimal Quantity, decimal UnitCost, DateOnly Date, Guid OffsetAccountId, string? Reference = null, string? Memo = null);
public sealed record IssueStockInput(Guid WarehouseId, Guid ItemId, decimal Quantity, DateOnly Date, Guid? ExpenseAccountId = null, string? Reference = null, string? Memo = null);
public sealed record AdjustStockInput(Guid WarehouseId, Guid ItemId, decimal QuantityDelta, DateOnly Date, decimal UnitCost = 0, Guid? OffsetAccountId = null, string? Reference = null, string? Memo = null);
public sealed record TransferStockInput(Guid FromWarehouseId, Guid ToWarehouseId, Guid ItemId, decimal Quantity, DateOnly Date, string? Reference = null, string? Memo = null);
public sealed record StockPolicyInput(Guid ItemId, decimal ReorderLevel, decimal ReorderQuantity);
