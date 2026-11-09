using System.Linq.Expressions;
using ERP.Modules.Manufacturing.Domain;

namespace ERP.Modules.Manufacturing.Features;

public record WorkOrderSummaryDto(
    Guid Id,
    Guid LegalEntityId,
    string WorkOrderNumber,
    string BomCode,
    string ProducedItemCode,
    string ProducedItemName,
    decimal PlannedQuantity,
    decimal CompletedQuantity,
    string Uom,
    DateOnly DueDate,
    WorkOrderStatus Status,
    decimal EstimatedTotalCost,
    decimal ActualTotalCost,
    DateTimeOffset CreatedAt)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<WorkOrder, WorkOrderSummaryDto>> Projection = w => new WorkOrderSummaryDto(
        w.Id, w.LegalEntityId, w.WorkOrderNumber, w.BomCode, w.ProducedItemCode, w.ProducedItemName,
        w.PlannedQuantity, w.CompletedQuantity, w.Uom, w.DueDate, w.Status,
        w.EstimatedTotalCost, w.ActualTotalCost, w.CreatedAt);
}
