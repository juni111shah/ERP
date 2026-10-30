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
    Refunded = 2
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

    public decimal TotalSales { get; set; }

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

    public DateTimeOffset Timestamp { get; set; }
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
