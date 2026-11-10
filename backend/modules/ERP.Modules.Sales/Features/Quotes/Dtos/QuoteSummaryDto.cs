using System.Linq.Expressions;
using ERP.Modules.Sales.Domain;

namespace ERP.Modules.Sales.Features;

public sealed record QuoteSummaryDto(
    Guid Id,
    string Number,
    Guid LegalEntityId,
    Guid? CustomerPartyId,
    string CustomerName,
    DateOnly QuoteDate,
    DateOnly? ValidUntil,
    string Currency,
    SalesQuoteStatus Status,
    decimal SubTotal,
    decimal TaxTotal,
    decimal DiscountTotal,
    decimal TotalAmount,
    int LineCount,
    Guid? ConvertedSalesOrderId,
    DateTime CreatedAt)
{
    /// <summary>The list row, projected in SQL.</summary>
    public static readonly Expression<Func<SalesQuote, QuoteSummaryDto>> Projection = q => new QuoteSummaryDto(
        q.Id, q.Number, q.LegalEntityId, q.CustomerPartyId, q.CustomerName, q.QuoteDate,
        q.ValidUntil, q.Currency, q.Status, q.SubTotal, q.TaxTotal, q.DiscountTotal,
        q.TotalAmount, q.Lines.Count, q.ConvertedSalesOrderId, q.CreatedAt);
}
