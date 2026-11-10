using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;

namespace ERP.Application.Portal;

internal sealed class ListPortalVendorPaymentsHandler(ISettlementRepository settlements, ICurrentUser user) : IQueryHandler<ListPortalVendorPaymentsQuery, IReadOnlyList<PortalPaymentDto>>
{
    public async Task<IReadOnlyList<PortalPaymentDto>> Handle(ListPortalVendorPaymentsQuery query, CancellationToken ct)
    {
        var me = PortalCaller.Vendor(user);
        return await settlements.ListIssuedPaymentsForPortalAsync(me, ct);
    }
}
