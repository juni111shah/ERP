using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>One lease with its rent schedule.</summary>
public sealed record GetLeaseQuery(Guid Id) : IQuery<LeaseDto>, IRequirePermission
{
    public string RequiredPermission => P.LeasesRead;
}
