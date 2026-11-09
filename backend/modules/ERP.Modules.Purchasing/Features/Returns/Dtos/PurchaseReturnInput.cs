namespace ERP.Modules.Purchasing.Features;

/// <summary>OrderLineId for a return against a purchase order (or a bill raised from one); BillLineId for a debit note on a bill with no order behind it.</summary>
public sealed record PurchaseReturnLineInput(Guid? OrderLineId, Guid? BillLineId, decimal Quantity);
/// <summary>At least one of PurchaseOrderId or BillId. WarehouseId defaults to where the order's goods were received.</summary>
public sealed record PurchaseReturnInput(Guid? PurchaseOrderId, Guid? BillId, DateOnly ReturnDate, string Reason, List<PurchaseReturnLineInput> Lines, Guid? WarehouseId = null, string? Notes = null);
