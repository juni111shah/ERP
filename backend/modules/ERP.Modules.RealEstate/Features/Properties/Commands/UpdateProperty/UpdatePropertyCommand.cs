using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Edits a property; its legal entity is fixed once it has units or leases.</summary>
public sealed record UpdatePropertyCommand(Guid Id, PropertyInput Input) : ICommand<PropertyDto>, IRequirePermission
{
    public string RequiredPermission => P.PropertiesManage;
}
