using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;

namespace ERP.Application.Portal;

internal sealed class GetPortalVendorStatementHandler(IBillRepository bills, ICurrentUser user) : IQueryHandler<GetPortalVendorStatementQuery, PortalPayablesDto>
{
    public async Task<PortalPayablesDto> Handle(GetPortalVendorStatementQuery query, CancellationToken ct)
    {
        var me = PortalCaller.Vendor(user);
        var open = await bills.ListOpenForPartyAsync(me, query.AsOf, ct);
        var lines = open.OrderBy(o => o.DueDate).Select(o => new PortalPayablesLine(o.Number, o.BillDate, o.DueDate, Math.Max(0, query.AsOf.DayNumber - o.DueDate.DayNumber), o.Total - o.Paid)).ToList();
        return new(query.AsOf, lines, lines.Sum(l => l.Outstanding));
    }
}
