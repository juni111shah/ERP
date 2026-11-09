using System.Linq.Expressions;
using ERP.Modules.Sales.Domain;

namespace ERP.Modules.Sales.Features;

public sealed record OrderSummaryDto(
    Guid Id,
    string Number,
    Guid LegalEntityId,
    Guid? QuoteId,
    string? QuoteNumber,
    Guid? CustomerPartyId,
    string CustomerName,
    string? CustomerReference,
    DateOnly OrderDate,
    DateOnly? ExpectedDeliveryDate,
    string Currency,
    SalesOrderStatus Status,
    decimal SubTotal,
    decimal TaxTotal,
    decimal DiscountTotal,
    decimal TotalAmount,
    int LineCount,
    Guid? InvoicedInvoiceId,
    DateTime CreatedAt)
{
    /// <summary>The list row, projected in SQL.</summary>
    public static readonly Expression<Func<SalesOrder, OrderSummaryDto>> Projection = o => new OrderSummaryDto(
        o.Id, o.Number, o.LegalEntityId, o.QuoteId, o.QuoteNumber, o.CustomerPartyId, o.CustomerName,
        o.CustomerReference, o.OrderDate, o.ExpectedDeliveryDate, o.Currency, o.Status,
        o.SubTotal, o.TaxTotal, o.DiscountTotal, o.TotalAmount, o.Lines.Count, o.InvoicedInvoiceId, o.CreatedAt);
}
