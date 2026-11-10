using ERP.Application.Common;

namespace ERP.Modules.Manufacturing.Features;

/// <summary>Finishes a work order: backflushes, receives the finished goods and posts the cost. ActualTotalCost is kept for older callers and ignored: the cost comes from what was issued and absorbed.</summary>
public record CompleteWorkOrderCommand(
    Guid Id,
    decimal CompletedQuantity,
    decimal ScrappedQuantity,
    decimal ActualTotalCost,
    string? Notes,
    DateOnly? CompletionDate = null,
    List<OperationHoursInput>? OperationHours = null) : ICommand<WorkOrderDto>;
