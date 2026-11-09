using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Registers a unit in a property (vacant), with its first asking rent and the property's unit count kept in step.</summary>
public sealed record CreateUnitCommand(UnitInput Input) : ICommand<UnitDto>, IRequirePermission
{
    public string RequiredPermission => P.UnitsManage;
}
