using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Organizations;

/// <summary>Adds a business unit (branch, store, site...) to a legal entity the caller may use.</summary>
public sealed record CreateBusinessUnitCommand(BusinessUnitInput Input) : ICommand<BusinessUnitDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.BusinessUnitsWrite;
}
