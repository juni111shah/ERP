using System.Linq.Expressions;
using ERP.Modules.Purchasing.Domain;

namespace ERP.Modules.Purchasing.Features;

public sealed record RequisitionSummaryDto(
    Guid Id,
    string Number,
    Guid LegalEntityId,
    string RequesterName,
    string? Department,
    DateOnly RequisitionDate,
    DateOnly? RequiredDate,
    PurchaseRequisitionStatus Status,
    decimal EstimatedTotal,
    int LineCount,
    Guid? ConvertedPurchaseOrderId,
    DateTime CreatedAt)
{
    /// <summary>The list row, projected in SQL.</summary>
    public static readonly Expression<Func<PurchaseRequisition, RequisitionSummaryDto>> Projection = r => new RequisitionSummaryDto(
        r.Id, r.Number, r.LegalEntityId, r.RequesterName, r.Department, r.RequisitionDate,
        r.RequiredDate, r.Status, r.EstimatedTotal, r.Lines.Count, r.ConvertedPurchaseOrderId, r.CreatedAt);
}
