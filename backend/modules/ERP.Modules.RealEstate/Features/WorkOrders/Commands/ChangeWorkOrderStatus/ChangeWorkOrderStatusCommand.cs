using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Moves a work order along its lifecycle. The permission depends on the target status (assign / complete / close / cancel).</summary>
public sealed record ChangeWorkOrderStatusCommand(Guid Id, WorkOrderStatus Status, decimal? ActualCost = null, string? ResolutionNotes = null) : ICommand<WorkOrderDto>, IRequirePermission
{
    public string RequiredPermission => Status switch
    {
        WorkOrderStatus.Assigned => P.WorkOrdersAssign,
        WorkOrderStatus.Completed => P.WorkOrdersComplete,
        WorkOrderStatus.Closed => P.WorkOrdersClose,
        WorkOrderStatus.Cancelled => P.WorkOrdersCancel,
        _ => P.WorkOrdersEdit,
    };
}
