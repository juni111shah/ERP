using System.Linq.Expressions;
using ERP.Modules.Purchasing.Domain;

namespace ERP.Modules.Purchasing.Features;

public sealed record GoodsReceiptSummaryDto(
    Guid Id,
    string Number,
    Guid LegalEntityId,
    Guid PurchaseOrderId,
    string PurchaseOrderNumber,
    string VendorName,
    DateOnly ReceiptDate,
    string ReceivedBy,
    string? DeliveryNoteNumber,
    int LineCount,
    DateTime CreatedAt)
{
    /// <summary>The list row, projected in SQL.</summary>
    public static readonly Expression<Func<GoodsReceipt, GoodsReceiptSummaryDto>> Projection = g => new GoodsReceiptSummaryDto(
        g.Id, g.Number, g.LegalEntityId, g.PurchaseOrderId, g.PurchaseOrderNumber, g.VendorName,
        g.ReceiptDate, g.ReceivedBy, g.DeliveryNoteNumber, g.Lines.Count, g.CreatedAt);
}
