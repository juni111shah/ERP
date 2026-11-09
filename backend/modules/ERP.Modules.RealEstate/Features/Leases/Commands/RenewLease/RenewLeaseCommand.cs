using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Creates the draft renewal of an active lease, starting the day after it ends.</summary>
public sealed record RenewLeaseCommand(Guid Id, RenewalInput Input) : ICommand<LeaseDto>, IRetryOnConcurrency, IRequirePermission
{
    public string RequiredPermission => P.LeasesRenew;
}
