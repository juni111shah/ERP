using ERP.Domain.Common;

namespace ERP.Modules.Pos.Domain;

public enum PosSessionStatus
{
    Open = 0,
    Closed = 1
}

public enum CashMovementType
{
    FloatIn = 0,
    PayIn = 1,
    PayOut = 2,
    DropOut = 3
}

public enum PosOrderStatus
{
    Completed = 0,
    Voided = 1,
    Parked = 2
}

public enum PosPaymentStatus
{
    Paid = 0,
    PartiallyPaid = 1,
    Refunded = 2,
    PartiallyRefunded = 3,
    OnAccount = 4,
    Unpaid = 5
}

public enum PosPaymentMethod
{
    Cash = 0,
    Card = 1,
    GiftCard = 2,
    Account = 3,
    Other = 4
}

public class PosRegister : LegalEntityScopedEntity, IHasCustomFields
{
    [Searchable, Sortable]
    public string Code { get; set; } = string.Empty;

    [Searchable, Sortable]
    public string Name { get; set; } = string.Empty;

    [Filterable]
    public Guid? WarehouseId { get; set; }

    public Guid? DefaultCashAccountId { get; set; }

    /// <summary>Where card takings land until the bank settles them. Falls back to the workspace's clearing or bank account.</summary>
    public Guid? CardAccountId { get; set; }

    /// <summary>Takes the cash over or short found when a shift closes.</summary>
    public Guid? VarianceAccountId { get; set; }

    /// <summary>The other side of cash paid out of or into the drawer, unless the movement names its own account.</summary>
    public Guid? ExpenseAccountId { get; set; }

    [Filterable]
    public bool IsActive { get; set; } = true;

    public string? ReceiptHeader { get; set; }

    public string? ReceiptFooter { get; set; }

    public string? CustomFields { get; set; }
}

public class PosSession : LegalEntityScopedEntity, IHasCustomFields
{
    [Filterable]
    public Guid RegisterId { get; set; }

    [Searchable, Sortable]
    public string RegisterName { get; set; } = string.Empty;

    [Filterable]
    public Guid OpenedByUserId { get; set; }

    [Searchable, Sortable]
    public string CashierName { get; set; } = string.Empty;

    [Sortable, Filterable]
    public DateTimeOffset OpenedAt { get; set; }

    public Guid? ClosedByUserId { get; set; }

    [Sortable]
    public DateTimeOffset? ClosedAt { get; set; }

    public decimal OpeningCash { get; set; }

    /// <summary>Completed sales of this shift, voided ones taken back out.</summary>
    public decimal TotalSales { get; set; }

    /// <summary>Money given back in this shift: customer returns and voided sales.</summary>
    public decimal TotalReturns { get; set; }

    public decimal TotalPayIn { get; set; }

    public decimal TotalPayOut { get; set; }

    public decimal ExpectedCash { get; set; }

    public decimal ActualCash { get; set; }

    public decimal CashDifference { get; set; }

    [Filterable]
    public PosSessionStatus Status { get; set; } = PosSessionStatus.Open;

    public string? Notes { get; set; }

    public string? CustomFields { get; set; }

    public List<CashDrawerTransaction> DrawerTransactions { get; set; } = new();

    public List<PosOrder> Orders { get; set; } = new();
}

public class CashDrawerTransaction : OrgEntity
{
    public Guid SessionId { get; set; }

    public CashMovementType Type { get; set; }

    public decimal Amount { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Guid PerformedByUserId { get; set; }

    public string? PerformedByName { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    /// <summary>The other side in the ledger. Null when the cash only moved inside cash on hand (a float, or a drop to the safe).</summary>
    public Guid? AccountId { get; set; }
}

public class PosOrder : LegalEntityScopedEntity, IHasCustomFields
{
    [Searchable, Sortable]
    public string Number { get; set; } = string.Empty;

    [Filterable]
    public Guid RegisterId { get; set; }

    [Filterable]
    public Guid SessionId { get; set; }

    [Filterable]
    public Guid? CustomerPartyId { get; set; }

    [Searchable, Sortable]
    public string CustomerName { get; set; } = "Walk-in Customer";

    [Sortable, Filterable]
    public DateTimeOffset OrderDate { get; set; }

    public decimal SubTotal { get; set; }

    public decimal TaxTotal { get; set; }

    public decimal DiscountTotal { get; set; }

    [Sortable]
    public decimal TotalAmount { get; set; }

    /// <summary>What customers have brought back so far, tax included.</summary>
    public decimal ReturnedTotal { get; set; }

    /// <summary>The issued invoice that carries the part of this sale put on the customer's account.</summary>
    public Guid? InvoiceId { get; set; }

    public string? InvoiceNumber { get; set; }

    [Filterable]
    public PosOrderStatus Status { get; set; } = PosOrderStatus.Completed;

    [Filterable]
    public PosPaymentStatus PaymentStatus { get; set; } = PosPaymentStatus.Paid;

    public Guid CashierUserId { get; set; }

    [Searchable]
    public string CashierName { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public string? CustomFields { get; set; }

    public List<PosOrderLine> Lines { get; set; } = new();

    public List<PosPayment> Payments { get; set; } = new();
}

public class PosOrderLine : OrgEntity
{
    public Guid PosOrderId { get; set; }

    public Guid? ItemId { get; set; }

    public string ItemCode { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal DiscountPercent { get; set; }

    public decimal TaxRate { get; set; }

    public decimal LineTotal { get; set; }

    public decimal ReturnedQuantity { get; set; }

    /// <summary>What returns have already given back for this line, tax included.</summary>
    public decimal ReturnedAmount { get; set; }
}

public class PosPayment : OrgEntity
{
    public Guid PosOrderId { get; set; }

    public PosPaymentMethod Method { get; set; }

    public decimal Amount { get; set; }

    public decimal TenderedAmount { get; set; }

    public decimal ChangeAmount { get; set; }

    public string? Reference { get; set; }

    public DateTimeOffset PaymentDate { get; set; }
}

/// <summary>Goods a customer brought back against a completed sale, and the money given back for them.</summary>
public class PosReturn : LegalEntityScopedEntity
{
    [Searchable, Sortable]
    public string Number { get; set; } = string.Empty;

    [Filterable]
    public Guid OriginalOrderId { get; set; }

    [Searchable]
    public string OriginalNumber { get; set; } = string.Empty;

    [Filterable]
    public Guid RegisterId { get; set; }

    [Filterable]
    public Guid SessionId { get; set; }

    [Filterable]
    public Guid? CustomerPartyId { get; set; }

    [Searchable, Sortable]
    public string CustomerName { get; set; } = "Walk-in Customer";

    [Sortable, Filterable]
    public DateTimeOffset ReturnDate { get; set; }

    public decimal SubTotal { get; set; }

    public decimal TaxTotal { get; set; }

    [Sortable]
    public decimal TotalAmount { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Guid CashierUserId { get; set; }

    [Searchable]
    public string CashierName { get; set; } = string.Empty;

    /// <summary>The issued credit note when the money went back onto the customer's account.</summary>
    public Guid? CreditNoteId { get; set; }

    public string? CreditNoteNumber { get; set; }

    public List<PosReturnLine> Lines { get; set; } = new();

    public List<PosReturnRefund> Refunds { get; set; } = new();
}

public class PosReturnLine : OrgEntity
{
    public Guid PosReturnId { get; set; }

    public Guid OrderLineId { get; set; }

    public Guid? ItemId { get; set; }

    public string ItemCode { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal DiscountPercent { get; set; }

    public decimal TaxRate { get; set; }

    public decimal NetAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal LineTotal { get; set; }

    /// <summary>False for damaged goods: the money goes back but nothing returns to the shelf.</summary>
    public bool Restock { get; set; } = true;
}

public class PosReturnRefund : OrgEntity
{
    public Guid PosReturnId { get; set; }

    public PosPaymentMethod Method { get; set; }

    public decimal Amount { get; set; }

    public string? Reference { get; set; }
}
