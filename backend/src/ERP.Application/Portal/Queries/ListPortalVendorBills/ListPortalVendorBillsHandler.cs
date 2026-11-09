using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;

namespace ERP.Application.Portal;

internal sealed class ListPortalVendorBillsHandler(IBillRepository bills, ICurrentUser user) : IQueryHandler<ListPortalVendorBillsQuery, IReadOnlyList<PortalBillDto>>
{
    public async Task<IReadOnlyList<PortalBillDto>> Handle(ListPortalVendorBillsQuery query, CancellationToken ct)
    {
        var me = PortalCaller.Vendor(user);
        return await bills.ListForPortalAsync(me, ct);
    }
}
