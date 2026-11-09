using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>One work order with its custom fields.</summary>
public sealed record GetWorkOrderQuery(Guid Id) : IQuery<WorkOrderDto>, IRequirePermission
{
    public string RequiredPermission => P.WorkOrdersRead;
}
