using ERP.Domain.Common;

namespace ERP.Modules.Purchasing.Domain;

public enum PurchaseOrderStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    PartiallyReceived = 3,
    Received = 4,
    Billed = 5,
    Cancelled = 6,
}

public enum PurchaseRequisitionStatus
{
    Draft = 0,
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3,
    Ordered = 4,
}

public enum PurchaseReturnStatus
{
    Draft = 0,
    Posted = 1,
    Voided = 2,
}

/// <summary>Goods sent back to a vendor, or a debit note on a bill. Posting takes the goods out of stock and reduces what is owed to the vendor.</summary>
public class PurchaseReturn : LegalEntityScopedEntity, IVoidable
{
    [Searchable, Sortable]
    public string Number { get; set; } = string.Empty;

    [Filterable]
    public Guid? PurchaseOrderId { get; set; }

    [Searchable]
    public string? PurchaseOrderNumber { get; set; }

    /// <summary>Set when the return was raised against one bill; otherwise it takes from the order's bills, newest first.</summary>
    [Filterable]
    public Guid? BillId { get; set; }

    [Filterable]
    public Guid VendorPartyId { get; set; }

    [Searchable, Sortable]
    public string VendorName { get; set; } = string.Empty;

    [Sortable, Filterable]
    public DateOnly ReturnDate { get; set; }

    [Filterable]
    public PurchaseReturnStatus Status { get; set; } = PurchaseReturnStatus.Draft;

    public string Reason { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public Guid? WarehouseId { get; set; }

    /// <summary>Net value of the billed part.</summary>
    public decimal SubTotal { get; set; }

    public decimal TaxTotal { get; set; }

    /// <summary>What payables go down by.</summary>
    [Sortable]
    public decimal TotalAmount { get; set; }

    /// <summary>What left stock, at its cost.</summary>
    public decimal StockValue { get; set; }

    public Guid? JournalEntryId { get; set; }

    public string? VoidReason { get; set; }

    public DateTime? VoidedAt { get; set; }

    public Guid? VoidedBy { get; set; }

    public List<PurchaseReturnLine> Lines { get; set; } = [];

    public List<PurchaseReturnBillShare> BillShares { get; set; } = [];
}

public class PurchaseReturnLine : OrgEntity
{
    public Guid ReturnId { get; set; }

    public Guid? OrderLineId { get; set; }

    public Guid? BillLineId { get; set; }

    public Guid? ItemId { get; set; }

    public string ItemCode { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Quantity { get; set; }
}

/// <summary>What a posted return took off one bill, so a void can put back exactly that.</summary>
public class PurchaseReturnBillShare : OrgEntity
{
    public Guid ReturnId { get; set; }

    public Guid BillId { get; set; }

    public Guid? OrderLineId { get; set; }

    public Guid? OrderBillLineId { get; set; }

    public decimal Quantity { get; set; }

    public decimal Amount { get; set; }

    /// <summary>The part of Amount that reduced the bill's balance. The rest, on a bill already paid, stays as a debit with the vendor.</summary>
    public decimal AppliedToBill { get; set; }
}

public class PurchaseRequisition : LegalEntityScopedEntity, IHasCustomFields
{
    [Searchable, Sortable]
    public string Number { get; set; } = string.Empty;

    [Searchable]
    public string RequesterName { get; set; } = string.Empty;

    [Searchable]
    public string? Department { get; set; }

    [Sortable, Filterable]
    public DateOnly RequisitionDate { get; set; }

    [Sortable, Filterable]
    public DateOnly? RequiredDate { get; set; }

    [Filterable]
    public PurchaseRequisitionStatus Status { get; set; } = PurchaseRequisitionStatus.Draft;

    [Sortable]
    public decimal EstimatedTotal { get; set; }

    public string? Reason { get; set; }

    public Guid? ConvertedPurchaseOrderId { get; set; }

    /// <summary>The approval request this requisition went through, when a policy covers it.</summary>
    public Guid? ApprovalRequestId { get; set; }

    public string? CustomFields { get; set; }

    public List<PurchaseRequisitionLine> Lines { get; set; } = [];
}

public class PurchaseRequisitionLine : OrgEntity
{
    public Guid RequisitionId { get; set; }

    public Guid? ItemId { get; set; }

    [Searchable]
    public string ItemCode { get; set; } = string.Empty;

    [Searchable]
    public string Description { get; set; } = string.Empty;

    public decimal Quantity { get; set; } = 1;

    public string UnitOfMeasure { get; set; } = "EA";

    public decimal EstimatedUnitPrice { get; set; }

    public decimal LineTotal { get; set; }
}

public class PurchaseOrder : LegalEntityScopedEntity, IHasCustomFields
{
    [Searchable, Sortable]
    public string Number { get; set; } = string.Empty;

    [Filterable]
    public Guid? RequisitionId { get; set; }

    [Filterable]
    public Guid? VendorPartyId { get; set; }

    [Searchable, Sortable]
    public string VendorName { get; set; } = string.Empty;

    public string? VendorContact { get; set; }

    public string? VendorReference { get; set; }

    [Sortable, Filterable]
    public DateOnly OrderDate { get; set; }

    [Sortable, Filterable]
    public DateOnly? ExpectedDeliveryDate { get; set; }

    public string Currency { get; set; } = "QAR";

    [Filterable]
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

    public decimal SubTotal { get; set; }

    public decimal TaxTotal { get; set; }

    public decimal DiscountTotal { get; set; }

    [Sortable]
    public decimal TotalAmount { get; set; }

    public string? PaymentTerms { get; set; }

    public string? DeliveryAddress { get; set; }

    public string? Notes { get; set; }

    /// <summary>The latest bill raised from this order. All of them are in <see cref="PurchaseOrderBillLine"/>.</summary>
    public Guid? InvoicedBillId { get; set; }

    /// <summary>The approval request this order went through, when a policy covers it.</summary>
    public Guid? ApprovalRequestId { get; set; }

    public string? CustomFields { get; set; }

    public List<PurchaseOrderLine> Lines { get; set; } = [];
}

public class PurchaseOrderLine : OrgEntity
{
    public Guid OrderId { get; set; }

    public Guid? ItemId { get; set; }

    [Searchable]
    public string ItemCode { get; set; } = string.Empty;

    [Searchable]
    public string Description { get; set; } = string.Empty;

    public decimal Quantity { get; set; } = 1;

    public decimal ReceivedQuantity { get; set; }

    public decimal BilledQuantity { get; set; }

    /// <summary>What has gone back to the vendor; already taken off ReceivedQuantity (and BilledQuantity for the billed part).</summary>
    public decimal ReturnedQuantity { get; set; }

    public string UnitOfMeasure { get; set; } = "EA";

    public decimal UnitPrice { get; set; }

    public decimal DiscountPercent { get; set; }

    public decimal TaxRate { get; set; }

    public decimal LineTotal { get; set; }
}

/// <summary>How much of a purchase order line one bill covers. A purchase order can be billed in parts; this is what links them.</summary>
public class PurchaseOrderBillLine : OrgEntity
{
    public Guid OrderId { get; set; }

    public Guid OrderLineId { get; set; }

    public Guid BillId { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>The part of Quantity billed against goods-received clearing; a bill posted later cannot clear more than this.</summary>
    public decimal ClearingQuantity { get; set; }

    /// <summary>What the vendor charged per unit on this bill (the override when there was one).</summary>
    public decimal UnitPrice { get; set; }

    public decimal TaxRate { get; set; }

    public bool TaxRecoverable { get; set; } = true;

    /// <summary>How much of Quantity has since gone back on a purchase return.</summary>
    public decimal ReturnedQuantity { get; set; }
}

public class GoodsReceipt : LegalEntityScopedEntity, IHasCustomFields
{
    [Searchable, Sortable]
    public string Number { get; set; } = string.Empty;

    [Filterable]
    public Guid PurchaseOrderId { get; set; }

    [Searchable]
    public string PurchaseOrderNumber { get; set; } = string.Empty;

    [Searchable, Sortable]
    public string VendorName { get; set; } = string.Empty;

    [Sortable, Filterable]
    public DateOnly ReceiptDate { get; set; }

    [Searchable]
    public string ReceivedBy { get; set; } = string.Empty;

    public string? DeliveryNoteNumber { get; set; }

    public string? Notes { get; set; }

    /// <summary>The warehouse asked for; empty means the legal entity's first active warehouse.</summary>
    public Guid? WarehouseId { get; set; }

    public string? CustomFields { get; set; }

    public List<GoodsReceiptLine> Lines { get; set; } = [];
}

public class GoodsReceiptLine : OrgEntity
{
    public Guid GoodsReceiptId { get; set; }

    public Guid PurchaseOrderLineId { get; set; }

    public Guid? ItemId { get; set; }

    [Searchable]
    public string ItemCode { get; set; } = string.Empty;

    [Searchable]
    public string Description { get; set; } = string.Empty;

    public decimal QuantityReceived { get; set; }

    public string UnitOfMeasure { get; set; } = "EA";

    /// <summary>True when this quantity moved no stock and put nothing on goods-received clearing (a line with no stocked item, or Inventory off), so billing it must not debit clearing.</summary>
    public bool NoStockMoved { get; set; }
}
