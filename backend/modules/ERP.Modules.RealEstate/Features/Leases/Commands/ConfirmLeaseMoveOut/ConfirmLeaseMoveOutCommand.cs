using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Confirms the tenant has moved out: the lease ends and the unit is vacated.</summary>
public sealed record ConfirmLeaseMoveOutCommand(Guid Id) : ICommand<LeaseDto>, IRequirePermission
{
    public string RequiredPermission => P.LeasesTerminate;
}
