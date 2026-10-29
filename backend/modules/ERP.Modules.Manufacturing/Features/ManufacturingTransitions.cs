using ERP.Application.Common;
using ERP.Modules.Manufacturing.Domain;

namespace ERP.Modules.Manufacturing.Features;

public static class ManufacturingTransitions
{
    public static readonly StatusTransitions<WorkOrderStatus> WorkOrder = new(
        new Dictionary<WorkOrderStatus, IEnumerable<WorkOrderStatus>>
        {
            [WorkOrderStatus.Draft] = [WorkOrderStatus.Planned, WorkOrderStatus.Cancelled],
            [WorkOrderStatus.Planned] = [WorkOrderStatus.InProgress, WorkOrderStatus.Cancelled],
            [WorkOrderStatus.InProgress] = [WorkOrderStatus.Completed, WorkOrderStatus.Cancelled],
            [WorkOrderStatus.Completed] = [],
            [WorkOrderStatus.Cancelled] = [],
        });
}
