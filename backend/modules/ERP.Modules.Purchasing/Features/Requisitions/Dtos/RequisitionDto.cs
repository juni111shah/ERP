using System.Text.Json.Nodes;
using ERP.Application.Customization;
using ERP.Modules.Purchasing.Domain;

namespace ERP.Modules.Purchasing.Features;

public sealed record RequisitionLineDto(
    Guid Id,
    Guid? ItemId,
    string ItemCode,
    string Description,
    decimal Quantity,
    string UnitOfMeasure,
    decimal EstimatedUnitPrice,
    decimal LineTotal);

public sealed record RequisitionDto(
    Guid Id,
    string Number,
    Guid LegalEntityId,
    string RequesterName,
    string? Department,
    DateOnly RequisitionDate,
    DateOnly? RequiredDate,
    PurchaseRequisitionStatus Status,
    decimal EstimatedTotal,
    string? Reason,
    Guid? ConvertedPurchaseOrderId,
    IReadOnlyList<RequisitionLineDto> Lines,
    JsonObject CustomFields,
    DateTime CreatedAt)
{
    /// <summary>The requisition with its lines and custom field values.</summary>
    public static async Task<RequisitionDto> FromAsync(PurchaseRequisition r, ICustomFieldService fields, CancellationToken ct)
    {
        var cf = await fields.ReadAsync(Docs.PurchaseRequisition, r, ct);
        return new RequisitionDto(
            r.Id,
            r.Number,
            r.LegalEntityId,
            r.RequesterName,
            r.Department,
            r.RequisitionDate,
            r.RequiredDate,
            r.Status,
            r.EstimatedTotal,
            r.Reason,
            r.ConvertedPurchaseOrderId,
            r.Lines.Select(l => new RequisitionLineDto(
                l.Id, l.ItemId, l.ItemCode, l.Description, l.Quantity, l.UnitOfMeasure,
                l.EstimatedUnitPrice, l.LineTotal)).ToList(),
            cf,
            r.CreatedAt);
    }
}
