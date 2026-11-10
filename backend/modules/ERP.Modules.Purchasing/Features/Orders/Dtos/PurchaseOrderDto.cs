using System.Text.Json.Nodes;
using ERP.Application.Customization;
using ERP.Modules.Purchasing.Domain;

namespace ERP.Modules.Purchasing.Features;

public sealed record PurchaseOrderLineDto(
    Guid Id,
    Guid? ItemId,
    string ItemCode,
    string Description,
    decimal Quantity,
    decimal ReceivedQuantity,
    decimal BilledQuantity,
    string UnitOfMeasure,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TaxRate,
    decimal LineTotal,
    decimal ReturnedQuantity = 0);

public sealed record PurchaseOrderDto(
    Guid Id,
    string Number,
    Guid LegalEntityId,
    Guid? RequisitionId,
    Guid? VendorPartyId,
    string VendorName,
    string? VendorContact,
    string? VendorReference,
    DateOnly OrderDate,
    DateOnly? ExpectedDeliveryDate,
    string Currency,
    PurchaseOrderStatus Status,
    decimal SubTotal,
    decimal TaxTotal,
    decimal DiscountTotal,
    decimal TotalAmount,
    string? PaymentTerms,
    string? DeliveryAddress,
    string? Notes,
    Guid? InvoicedBillId,
    IReadOnlyList<PurchaseOrderLineDto> Lines,
    JsonObject CustomFields,
    DateTime CreatedAt,
    IReadOnlyList<Guid>? BillIds = null)
{
    /// <summary>The order with its lines and custom field values.</summary>
    public static async Task<PurchaseOrderDto> FromAsync(PurchaseOrder o, ICustomFieldService fields, CancellationToken ct)
    {
        var cf = await fields.ReadAsync(Docs.PurchaseOrder, o, ct);
        return new PurchaseOrderDto(
            o.Id,
            o.Number,
            o.LegalEntityId,
            o.RequisitionId,
            o.VendorPartyId,
            o.VendorName,
            o.VendorContact,
            o.VendorReference,
            o.OrderDate,
            o.ExpectedDeliveryDate,
            o.Currency,
            o.Status,
            o.SubTotal,
            o.TaxTotal,
            o.DiscountTotal,
            o.TotalAmount,
            o.PaymentTerms,
            o.DeliveryAddress,
            o.Notes,
            o.InvoicedBillId,
            o.Lines.Select(l => new PurchaseOrderLineDto(
                l.Id, l.ItemId, l.ItemCode, l.Description, l.Quantity, l.ReceivedQuantity,
                l.BilledQuantity, l.UnitOfMeasure, l.UnitPrice, l.DiscountPercent, l.TaxRate, l.LineTotal, l.ReturnedQuantity)).ToList(),
            cf,
            o.CreatedAt);
    }
}
