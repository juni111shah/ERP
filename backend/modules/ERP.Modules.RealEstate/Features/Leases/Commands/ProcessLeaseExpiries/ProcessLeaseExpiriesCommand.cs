using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Daily job body for the current workspace: ends or holds over expired leases and releases expired reservations. Idempotent.</summary>
public sealed record ProcessLeaseExpiriesCommand : ICommand<ExpiryRunResultDto>, IRequirePermission
{
    public string RequiredPermission => P.LeasesActivate;
}
