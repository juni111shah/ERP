using System.Text.Json.Nodes;
using ERP.Application.Customization;
using ERP.Modules.Sales.Domain;

namespace ERP.Modules.Sales.Features;

public sealed record OrderLineDto(
    Guid Id,
    Guid? ItemId,
    string ItemCode,
    string Description,
    decimal Quantity,
    decimal DeliveredQuantity,
    decimal InvoicedQuantity,
    string UnitOfMeasure,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TaxRate,
    decimal LineTotal,
    decimal ReturnedQuantity = 0);

public sealed record OrderDto(
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
    string? PaymentTerms,
    string? ShippingAddress,
    string? Notes,
    Guid? InvoicedInvoiceId,
    IReadOnlyList<OrderLineDto> Lines,
    JsonObject CustomFields,
    DateTime CreatedAt,
    IReadOnlyList<string>? Warnings = null)
{
    /// <summary>The order with its lines and custom field values.</summary>
    public static async Task<OrderDto> FromAsync(SalesOrder o, ICustomFieldService fields, CancellationToken ct)
    {
        var cf = await fields.ReadAsync(Docs.SalesOrder, o, ct);
        return new OrderDto(
            o.Id,
            o.Number,
            o.LegalEntityId,
            o.QuoteId,
            o.QuoteNumber,
            o.CustomerPartyId,
            o.CustomerName,
            o.CustomerReference,
            o.OrderDate,
            o.ExpectedDeliveryDate,
            o.Currency,
            o.Status,
            o.SubTotal,
            o.TaxTotal,
            o.DiscountTotal,
            o.TotalAmount,
            o.PaymentTerms,
            o.ShippingAddress,
            o.Notes,
            o.InvoicedInvoiceId,
            o.Lines.Select(l => new OrderLineDto(
                l.Id, l.ItemId, l.ItemCode, l.Description, l.Quantity, l.DeliveredQuantity,
                l.InvoicedQuantity, l.UnitOfMeasure, l.UnitPrice, l.DiscountPercent, l.TaxRate, l.LineTotal, l.ReturnedQuantity)).ToList(),
            cf,
            o.CreatedAt);
    }
}
