using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Repositories;

namespace ERP.Modules.RealEstate.Features;

internal sealed class ListVendorWorkOrdersHandler(ICurrentUser user, IWorkOrderRepository workOrders) : IQueryHandler<ListVendorWorkOrdersQuery, IReadOnlyList<VendorWorkOrderDto>>
{
    public async Task<IReadOnlyList<VendorWorkOrderDto>> Handle(ListVendorWorkOrdersQuery q, CancellationToken ct)
    {
        var me = VendorPortalUser.Me(user);
        var rows = await workOrders.ListLatestOfVendorAsync(me, ct);
        return rows.Select(VendorWorkOrderDto.From).ToList();
    }
}
