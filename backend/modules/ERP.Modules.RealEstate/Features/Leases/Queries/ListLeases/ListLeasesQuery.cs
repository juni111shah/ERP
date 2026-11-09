using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Leases of the caller's workspace (standard search / filter / sort / paging) with unit, property and tenant names.</summary>
public sealed record ListLeasesQuery : PagedQuery, IQuery<PagedResult<LeaseListItem>>, IRequirePermission
{
    public string RequiredPermission => P.LeasesRead;
}
