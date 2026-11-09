using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Edits a unit (it stays in its property); a changed asking rent is recorded in the rent history.</summary>
public sealed record UpdateUnitCommand(Guid Id, UnitInput Input) : ICommand<UnitDto>, IRequirePermission
{
    public string RequiredPermission => P.UnitsManage;
}
