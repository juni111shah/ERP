using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>The asking rent history of a unit, latest first.</summary>
public sealed record GetUnitRentHistoryQuery(Guid UnitId) : IQuery<IReadOnlyList<UnitRentRateDto>>, IRequirePermission
{
    public string RequiredPermission => P.UnitsRead;
}
