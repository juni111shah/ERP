using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Raises a work order on a property (and optionally a unit and lease), numbered from the work order sequence.</summary>
public sealed record CreateWorkOrderCommand(WorkOrderInput Input) : ICommand<WorkOrderDto>, IRequirePermission
{
    public string RequiredPermission => P.WorkOrdersCreate;
}
