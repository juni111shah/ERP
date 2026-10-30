using ERP.Domain.Common;

namespace ERP.Modules.Sales.Domain;

public enum SalesQuoteStatus
{
    Draft = 0,
    Sent = 1,
    Accepted = 2,
    Declined = 3,
    Expired = 4,
    Converted = 5,
}

public enum SalesOrderStatus
{
    Draft = 0,
    Confirmed = 1,
    Processing = 2,
    Delivered = 3,
    Invoiced = 4,
    Cancelled = 5,
}

public class SalesQuote : LegalEntityScopedEntity, IHasCustomFields
{
    [Searchable, Sortable]
    public string Number { get; set; } = string.Empty;

    [Filterable]
    public Guid? CustomerPartyId { get; set; }

    [Searchable, Sortable]
    public string CustomerName { get; set; } = string.Empty;

    public string? CustomerEmail { get; set; }

    public string? CustomerPhone { get; set; }

    [Sortable, Filterable]
    public DateOnly QuoteDate { get; set; }

    [Sortable, Filterable]
    public DateOnly? ValidUntil { get; set; }

    public string Currency { get; set; } = "QAR";

    [Filterable]
    public SalesQuoteStatus Status { get; set; } = SalesQuoteStatus.Draft;

    public decimal SubTotal { get; set; }

    public decimal TaxTotal { get; set; }

    public decimal DiscountTotal { get; set; }

    [Sortable]
    public decimal TotalAmount { get; set; }

    public string? Notes { get; set; }

    public string? Terms { get; set; }

    public Guid? ConvertedSalesOrderId { get; set; }

    public string? CustomFields { get; set; }

    public List<SalesQuoteLine> Lines { get; set; } = [];
}

public class SalesQuoteLine : OrgEntity
{
    public Guid QuoteId { get; set; }

    public Guid? ItemId { get; set; }

    [Searchable]
    public string ItemCode { get; set; } = string.Empty;

    [Searchable]
    public string Description { get; set; } = string.Empty;

    public decimal Quantity { get; set; } = 1;

    public string UnitOfMeasure { get; set; } = "EA";

    public decimal UnitPrice { get; set; }

    public decimal DiscountPercent { get; set; }

    public decimal TaxRate { get; set; }

    public decimal LineTotal { get; set; }
}

public class SalesOrder : LegalEntityScopedEntity, IHasCustomFields
{
    [Searchable, Sortable]
    public string Number { get; set; } = string.Empty;

    [Filterable]
    public Guid? QuoteId { get; set; }

    [Searchable]
    public string? QuoteNumber { get; set; }

    [Filterable]
    public Guid? CustomerPartyId { get; set; }

    [Searchable, Sortable]
    public string CustomerName { get; set; } = string.Empty;

    [Searchable]
    public string? CustomerReference { get; set; }

    [Sortable, Filterable]
    public DateOnly OrderDate { get; set; }

    [Sortable, Filterable]
    public DateOnly? ExpectedDeliveryDate { get; set; }

    public string Currency { get; set; } = "QAR";

    [Filterable]
    public SalesOrderStatus Status { get; set; } = SalesOrderStatus.Draft;

    public decimal SubTotal { get; set; }

    public decimal TaxTotal { get; set; }

    public decimal DiscountTotal { get; set; }

    [Sortable]
    public decimal TotalAmount { get; set; }

    public string? PaymentTerms { get; set; }

    public string? ShippingAddress { get; set; }

    public string? Notes { get; set; }

    public Guid? InvoicedInvoiceId { get; set; }

    public string? CustomFields { get; set; }

    public List<SalesOrderLine> Lines { get; set; } = [];
}

public class SalesOrderLine : OrgEntity
{
    public Guid OrderId { get; set; }

    public Guid? ItemId { get; set; }

    [Searchable]
    public string ItemCode { get; set; } = string.Empty;

    [Searchable]
    public string Description { get; set; } = string.Empty;

    public decimal Quantity { get; set; } = 1;

    public decimal DeliveredQuantity { get; set; }

    public decimal InvoicedQuantity { get; set; }

    public string UnitOfMeasure { get; set; } = "EA";

    public decimal UnitPrice { get; set; }

    public decimal DiscountPercent { get; set; }

    public decimal TaxRate { get; set; }

    public decimal LineTotal { get; set; }
}
