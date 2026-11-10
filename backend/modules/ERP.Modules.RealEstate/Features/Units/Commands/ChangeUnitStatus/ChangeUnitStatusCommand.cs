using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Reserve / release / make unavailable / make available (Occupied and Vacant-by-lease are set only by leases).</summary>
public sealed record ChangeUnitStatusCommand(Guid Id, UnitAction Action, DateOnly? Until = null, string? Reason = null) : ICommand<UnitDto>, IRequirePermission
{
    public string RequiredPermission => Action is UnitAction.Reserve or UnitAction.ReleaseReservation ? P.UnitsReserve : P.UnitsManage;
}
