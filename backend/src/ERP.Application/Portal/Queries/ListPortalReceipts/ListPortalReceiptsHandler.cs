using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;

namespace ERP.Application.Portal;

internal sealed class ListPortalReceiptsHandler(ISettlementRepository settlements, ICurrentUser user) : IQueryHandler<ListPortalReceiptsQuery, IReadOnlyList<PortalReceiptDto>>
{
    public async Task<IReadOnlyList<PortalReceiptDto>> Handle(ListPortalReceiptsQuery query, CancellationToken ct)
    {
        var me = PortalCaller.Customer(user);
        return await settlements.ListIssuedReceiptsForPortalAsync(me, ct);
    }
}
