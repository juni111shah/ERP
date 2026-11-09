using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class GetVendorLedgerHandler(IBillRepository bills, ISettlementRepository settlements, IReportCurrencyGuard currencies) : IQueryHandler<GetVendorLedgerQuery, PartyLedgerDto>
{
    public async Task<PartyLedgerDto> Handle(GetVendorLedgerQuery query, CancellationToken ct)
    {
        await currencies.EnsureSingleCurrencyAsync(query.LegalEntityId, ct);
        var q = query;

        // 1. Opening balance: bills and payments before the range, and voids taken back before it.
        var openingDocs = await bills.StatementTotalBeforeAsync(q.PartyId, q.From, q.To, q.LegalEntityId, ct);
        var openingCash = await settlements.StatementTotalBeforeAsync(InstrumentDirection.Issued, q.PartyId, q.From, q.To, q.LegalEntityId, ct);
        var voidedDocs = await bills.ListVoidedAfterLockAsync(q.PartyId, q.To, q.LegalEntityId, ct);
        var voidedCash = await settlements.ListVoidedAfterLockAsync(InstrumentDirection.Issued, q.PartyId, q.To, q.LegalEntityId, ct);
        var openingVoids = voidedCash.Where(v => v.On < q.From).Sum(v => v.Amount) - voidedDocs.Where(v => v.On < q.From).Sum(v => v.Total);

        // 2. The bills, payments and voids in the range.
        var inRange = await bills.ListStatementRowsAsync(q.PartyId, q.From, q.To, q.LegalEntityId, ct);
        var paid = await settlements.ListStatementRowsAsync(InstrumentDirection.Issued, q.PartyId, q.From, q.To, q.LegalEntityId, ct);
        var lines = inRange.Select(b => new PartyLedgerLine(b.BillDate, "bill", b.Id, b.Number, b.VendorReference, b.CurrencyCode, 0, b.Total, 0))
            .Concat(paid.Select(s => new PartyLedgerLine(s.Date, "payment", s.Id, s.Number, s.Reference, s.CurrencyCode, s.Amount, 0, 0)))
            .Concat(voidedDocs.Where(v => v.On >= q.From).Select(v => new PartyLedgerLine(v.On, PartyLedger.VoidKind, v.Id, v.Number, v.VendorReference, v.CurrencyCode, v.Total, 0, 0)))
            .Concat(voidedCash.Where(v => v.On >= q.From).Select(v => new PartyLedgerLine(v.On, PartyLedger.VoidKind, v.Id, v.Number, v.Reference, v.CurrencyCode, 0, v.Amount, 0)));

        // 3. Running balances (positive = owed to the vendor).
        var name = inRange.Select(b => b.PartyName).Concat(paid.Select(s => s.PartyName)).FirstOrDefault(n => !string.IsNullOrEmpty(n));
        return PartyLedger.Build(q.PartyId, name, "vendor", q.From, q.To, openingDocs - openingCash + openingVoids, lines, debitRaises: false);
    }
}
