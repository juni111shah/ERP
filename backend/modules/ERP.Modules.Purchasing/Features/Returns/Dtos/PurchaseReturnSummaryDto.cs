using System.Linq.Expressions;
using ERP.Modules.Purchasing.Domain;

namespace ERP.Modules.Purchasing.Features;

public sealed record PurchaseReturnSummaryDto(
    Guid Id, string Number, Guid LegalEntityId, Guid? PurchaseOrderId, string? PurchaseOrderNumber, Guid? BillId, Guid VendorPartyId, string VendorName,
    DateOnly ReturnDate, PurchaseReturnStatus Status, decimal TotalAmount, decimal StockValue, int LineCount, DateTime CreatedAt)
{
    /// <summary>The list row, projected in SQL.</summary>
    public static readonly Expression<Func<PurchaseReturn, PurchaseReturnSummaryDto>> Projection = r => new PurchaseReturnSummaryDto(
        r.Id, r.Number, r.LegalEntityId, r.PurchaseOrderId, r.PurchaseOrderNumber, r.BillId, r.VendorPartyId, r.VendorName,
        r.ReturnDate, r.Status, r.TotalAmount, r.StockValue, r.Lines.Count, r.CreatedAt);
}
