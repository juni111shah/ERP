using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Organizations;

/// <summary>Changes a business unit. It cannot move to another legal entity, and its code stays unique within the entity.</summary>
public sealed record UpdateBusinessUnitCommand(Guid Id, BusinessUnitInput Input) : ICommand<BusinessUnitDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.BusinessUnitsWrite;
}
