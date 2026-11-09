using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Creates a draft lease on a vacant or reserved unit; with ActivateNow it is activated in the same transaction.</summary>
public sealed record CreateLeaseCommand(LeaseInput Input) : ICommand<LeaseDto>, IRetryOnConcurrency, IRequirePermission
{
    public string RequiredPermission => Input.ActivateNow ? P.LeasesActivate : P.LeasesCreate;
}
