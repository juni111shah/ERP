using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Creates the units of a range of floors in one go, numbered floor + position ("1406").</summary>
public sealed record BulkCreateUnitsCommand(BulkUnitsInput Input) : ICommand<IReadOnlyList<UnitDto>>, IRequirePermission
{
    public string RequiredPermission => P.UnitsManage;
}
