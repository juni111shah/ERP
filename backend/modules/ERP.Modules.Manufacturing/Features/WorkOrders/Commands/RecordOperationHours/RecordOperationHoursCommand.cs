using ERP.Application.Common;
using ERP.Modules.Manufacturing.Domain;

namespace ERP.Modules.Manufacturing.Features;

/// <summary>Records the hours spent on one operation of a work order in progress.</summary>
public record RecordOperationHoursCommand(
    Guid WorkOrderId,
    Guid OperationId,
    decimal ActualHours,
    OperationStatus? Status = null) : ICommand<WorkOrderDto>;
