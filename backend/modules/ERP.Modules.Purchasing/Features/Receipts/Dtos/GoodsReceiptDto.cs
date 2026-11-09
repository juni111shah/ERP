using System.Text.Json.Nodes;
using ERP.Application.Customization;
using ERP.Modules.Purchasing.Domain;

namespace ERP.Modules.Purchasing.Features;

public sealed record GoodsReceiptLineDto(
    Guid Id,
    Guid PurchaseOrderLineId,
    Guid? ItemId,
    string ItemCode,
    string Description,
    decimal QuantityReceived,
    string UnitOfMeasure);

public sealed record GoodsReceiptDto(
    Guid Id,
    string Number,
    Guid LegalEntityId,
    Guid PurchaseOrderId,
    string PurchaseOrderNumber,
    string VendorName,
    DateOnly ReceiptDate,
    string ReceivedBy,
    string? DeliveryNoteNumber,
    string? Notes,
    IReadOnlyList<GoodsReceiptLineDto> Lines,
    JsonObject CustomFields,
    DateTime CreatedAt,
    Guid? WarehouseId = null)
{
    /// <summary>The receipt with its lines and custom field values.</summary>
    public static async Task<GoodsReceiptDto> FromAsync(GoodsReceipt g, ICustomFieldService fields, CancellationToken ct)
    {
        var cf = await fields.ReadAsync(Docs.GoodsReceipt, g, ct);
        return new GoodsReceiptDto(
            g.Id,
            g.Number,
            g.LegalEntityId,
            g.PurchaseOrderId,
            g.PurchaseOrderNumber,
            g.VendorName,
            g.ReceiptDate,
            g.ReceivedBy,
            g.DeliveryNoteNumber,
            g.Notes,
            g.Lines.Select(l => new GoodsReceiptLineDto(
                l.Id, l.PurchaseOrderLineId, l.ItemId, l.ItemCode, l.Description, l.QuantityReceived, l.UnitOfMeasure)).ToList(),
            cf,
            g.CreatedAt,
            g.WarehouseId);
    }
}
