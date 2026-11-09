using ERP.Application.Common;

namespace ERP.Application.Finance;

internal sealed class GetCashPositionHandler(IAccountRepository accounts, IJournalEntryRepository journals, IReportCurrencyGuard currencies) : IQueryHandler<GetCashPositionQuery, CashPositionDto>
{
    public async Task<CashPositionDto> Handle(GetCashPositionQuery query, CancellationToken ct)
    {
        // 1. One currency only; the bank and cash accounts and their ledger movements.
        await currencies.EnsureSingleCurrencyAsync(query.LegalEntityId, ct);
        var cash = await accounts.ListCashAndBankAsync(ct);
        var ids = cash.Select(c => c.Id).ToList();
        var movements = await journals.ListMovementsAsync(ids, query.AsOf, query.LegalEntityId, ct);

        // 2. Balance per account and the total.
        var rows = cash.Select(c => new CashPositionRow(c.Id, c.Code, c.Name, c.IsBank, movements.Where(m => m.AccountId == c.Id).Sum(m => m.Debit - m.Credit))).ToList();
        return new CashPositionDto(query.AsOf, rows, rows.Sum(r => r.Balance));
    }
}
