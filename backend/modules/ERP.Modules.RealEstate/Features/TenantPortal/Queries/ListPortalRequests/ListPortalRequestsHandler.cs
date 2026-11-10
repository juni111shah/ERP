using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Repositories;

namespace ERP.Modules.RealEstate.Features;

internal sealed class ListPortalRequestsHandler(ICurrentUser user, ILeaseRepository leases, IWorkOrderRepository workOrders) : IQueryHandler<ListPortalRequestsQuery, IReadOnlyList<PortalRequestDto>>
{
    public async Task<IReadOnlyList<PortalRequestDto>> Handle(ListPortalRequestsQuery q, CancellationToken ct)
    {
        var me = TenantPortalUser.Me(user);
        var leaseIds = await leases.ListIdsOfTenantAsync(me, ct);
        var rows = await workOrders.ListLatestOfLeasesAsync(leaseIds, ct);
        return rows.Select(PortalRequestDto.From).ToList();
    }
}
