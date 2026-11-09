using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Edits an open work order (vendor, technician, schedule, costs...).</summary>
public sealed record UpdateWorkOrderCommand(Guid Id, WorkOrderInput Input) : ICommand<WorkOrderDto>, IRequirePermission
{
    public string RequiredPermission => P.WorkOrdersEdit;
}
