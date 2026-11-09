using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Creates a property in a legal entity the caller may use, numbered from the property sequence.</summary>
public sealed record CreatePropertyCommand(PropertyInput Input) : ICommand<PropertyDto>, IRequirePermission
{
    public string RequiredPermission => P.PropertiesManage;
}
