using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class GetCustomerLedgerHandler(IInvoiceRepository invoices, ISettlementRepository settlements, IReportCurrencyGuard currencies) : IQueryHandler<GetCustomerLedgerQuery, PartyLedgerDto>
{
    public async Task<PartyLedgerDto> Handle(GetCustomerLedgerQuery query, CancellationToken ct)
    {
        await currencies.EnsureSingleCurrencyAsync(query.LegalEntityId, ct);
        var q = query;

        // 1. Opening balance: documents and receipts before the range, and voids taken back before it.
        var openingDocs = await invoices.StatementTotalBeforeAsync(q.PartyId, q.From, q.To, q.LegalEntityId, ct);
        var openingCash = await settlements.StatementTotalBeforeAsync(InstrumentDirection.Received, q.PartyId, q.From, q.To, q.LegalEntityId, ct);
        var voidedDocs = await invoices.ListVoidedAfterLockAsync(q.PartyId, q.To, q.LegalEntityId, ct);
        var voidedCash = await settlements.ListVoidedAfterLockAsync(InstrumentDirection.Received, q.PartyId, q.To, q.LegalEntityId, ct);
        var openingVoids = voidedDocs.Where(v => v.On < q.From).Sum(v => v.IsCreditNote ? v.Total : -v.Total) + voidedCash.Where(v => v.On < q.From).Sum(v => v.Amount);

        // 2. The documents, receipts and voids in the range.
        var inRange = await invoices.ListStatementRowsAsync(q.PartyId, q.From, q.To, q.LegalEntityId, ct);
        var received = await settlements.ListStatementRowsAsync(InstrumentDirection.Received, q.PartyId, q.From, q.To, q.LegalEntityId, ct);
        var lines = inRange.Select(i => new PartyLedgerLine(i.InvoiceDate, i.IsCreditNote ? "creditNote" : "invoice", i.Id, i.Number, null, i.CurrencyCode,
                i.IsCreditNote ? 0 : i.Total, i.IsCreditNote ? i.Total : 0, 0))
            .Concat(received.Select(s => new PartyLedgerLine(s.Date, "receipt", s.Id, s.Number, s.Reference, s.CurrencyCode, 0, s.Amount, 0)))
            .Concat(voidedDocs.Where(v => v.On >= q.From).Select(v => new PartyLedgerLine(v.On, PartyLedger.VoidKind, v.Id, v.Number, null, v.CurrencyCode, v.IsCreditNote ? v.Total : 0, v.IsCreditNote ? 0 : v.Total, 0)))
            .Concat(voidedCash.Where(v => v.On >= q.From).Select(v => new PartyLedgerLine(v.On, PartyLedger.VoidKind, v.Id, v.Number, v.Reference, v.CurrencyCode, v.Amount, 0, 0)));

        // 3. Running balances (positive = the customer owes).
        var name = inRange.Select(i => i.PartyName).Concat(received.Select(s => s.PartyName)).FirstOrDefault(n => !string.IsNullOrEmpty(n));
        return PartyLedger.Build(q.PartyId, name, "customer", q.From, q.To, openingDocs - openingCash + openingVoids, lines, debitRaises: true);
    }
}
