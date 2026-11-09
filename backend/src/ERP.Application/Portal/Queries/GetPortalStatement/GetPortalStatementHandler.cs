using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;

namespace ERP.Application.Portal;

internal sealed class GetPortalStatementHandler(IInvoiceRepository invoices, ISettlementRepository settlements, ICurrentUser user) : IQueryHandler<GetPortalStatementQuery, PortalStatementDto>
{
    public async Task<PortalStatementDto> Handle(GetPortalStatementQuery query, CancellationToken ct)
    {
        // 1. The customer's open invoices and credit notes, and what is left unallocated on their receipts.
        var me = PortalCaller.Customer(user);
        var open = await invoices.ListOpenForPartyAsync(me, query.AsOf, ct);
        var unallocated = (await settlements.ListUnallocatedReceiptAmountsAsync(me, ct)).Sum();

        // 2. Oldest due first; credit notes reduce the balance.
        var lines = open.OrderBy(o => o.DueDate).Select(o => new PortalStatementLine(o.Number, o.IsCreditNote, o.InvoiceDate, o.DueDate, Math.Max(0, query.AsOf.DayNumber - o.DueDate.DayNumber), o.Total - o.Paid)).ToList();
        return new(query.AsOf, lines, unallocated, open.Sum(o => (o.IsCreditNote ? -1 : 1) * (o.Total - o.Paid)) - unallocated);
    }
}
