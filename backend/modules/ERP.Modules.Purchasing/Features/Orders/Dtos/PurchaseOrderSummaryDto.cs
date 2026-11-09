using System.Linq.Expressions;
using ERP.Modules.Purchasing.Domain;

namespace ERP.Modules.Purchasing.Features;

public sealed record PurchaseOrderSummaryDto(
    Guid Id,
    string Number,
    Guid LegalEntityId,
    Guid? VendorPartyId,
    string VendorName,
    string? VendorReference,
    DateOnly OrderDate,
    DateOnly? ExpectedDeliveryDate,
    string Currency,
    PurchaseOrderStatus Status,
    decimal SubTotal,
    decimal TaxTotal,
    decimal DiscountTotal,
    decimal TotalAmount,
    int LineCount,
    Guid? InvoicedBillId,
    DateTime CreatedAt)
{
    /// <summary>The list row, projected in SQL.</summary>
    public static readonly Expression<Func<PurchaseOrder, PurchaseOrderSummaryDto>> Projection = o => new PurchaseOrderSummaryDto(
        o.Id, o.Number, o.LegalEntityId, o.VendorPartyId, o.VendorName, o.VendorReference,
        o.OrderDate, o.ExpectedDeliveryDate, o.Currency, o.Status, o.SubTotal, o.TaxTotal,
        o.DiscountTotal, o.TotalAmount, o.Lines.Count, o.InvoicedBillId, o.CreatedAt);
}
