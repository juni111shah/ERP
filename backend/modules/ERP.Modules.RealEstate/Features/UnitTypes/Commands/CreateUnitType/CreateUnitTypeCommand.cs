using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Creates a unit type; codes are unique (upper-cased).</summary>
public sealed record CreateUnitTypeCommand(UnitTypeInput Input) : ICommand<UnitTypeDto>, IRequirePermission
{
    public string RequiredPermission => P.UnitTypesManage;
}
