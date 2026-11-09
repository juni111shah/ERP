using ERP.Application.Common;

namespace ERP.Modules.Manufacturing.Features;

/// <summary>Plans a work order from an active BOM, estimated at today's component costs.</summary>
public record CreateWorkOrderCommand(
    Guid LegalEntityId,
    Guid BomId,
    decimal PlannedQuantity,
    Guid WarehouseId,
    DateOnly StartDate,
    DateOnly DueDate,
    string? Notes,
    List<WorkOrderOperationInput>? Operations,
    string? CustomFields) : ICommand<WorkOrderDto>;
