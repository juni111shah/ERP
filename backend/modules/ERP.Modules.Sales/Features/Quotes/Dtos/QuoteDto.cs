using System.Text.Json.Nodes;
using ERP.Application.Customization;
using ERP.Modules.Sales.Domain;

namespace ERP.Modules.Sales.Features;

public sealed record QuoteLineDto(
    Guid Id,
    Guid? ItemId,
    string ItemCode,
    string Description,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TaxRate,
    decimal LineTotal);

public sealed record QuoteDto(
    Guid Id,
    string Number,
    Guid LegalEntityId,
    Guid? CustomerPartyId,
    string CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    DateOnly QuoteDate,
    DateOnly? ValidUntil,
    string Currency,
    SalesQuoteStatus Status,
    decimal SubTotal,
    decimal TaxTotal,
    decimal DiscountTotal,
    decimal TotalAmount,
    string? Notes,
    string? Terms,
    Guid? ConvertedSalesOrderId,
    IReadOnlyList<QuoteLineDto> Lines,
    JsonObject CustomFields,
    DateTime CreatedAt)
{
    /// <summary>The quote with its lines and custom field values.</summary>
    public static async Task<QuoteDto> FromAsync(SalesQuote q, ICustomFieldService fields, CancellationToken ct)
    {
        var cf = await fields.ReadAsync(Docs.Quote, q, ct);
        return new QuoteDto(
            q.Id,
            q.Number,
            q.LegalEntityId,
            q.CustomerPartyId,
            q.CustomerName,
            q.CustomerEmail,
            q.CustomerPhone,
            q.QuoteDate,
            q.ValidUntil,
            q.Currency,
            q.Status,
            q.SubTotal,
            q.TaxTotal,
            q.DiscountTotal,
            q.TotalAmount,
            q.Notes,
            q.Terms,
            q.ConvertedSalesOrderId,
            q.Lines.Select(l => new QuoteLineDto(
                l.Id, l.ItemId, l.ItemCode, l.Description, l.Quantity, l.UnitOfMeasure,
                l.UnitPrice, l.DiscountPercent, l.TaxRate, l.LineTotal)).ToList(),
            cf,
            q.CreatedAt);
    }
}
