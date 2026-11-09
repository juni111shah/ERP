namespace ERP.Application.Finance;

/// <summary>Opening balance, inflows, outflows and closing balance of each active bank and cash account for a period, and the movement by source.</summary>
public sealed class CashMovementsCalculator(IJournalEntryRepository journals, IAccountRepository accounts, IReportCurrencyGuard currencies)
{
    public async Task<CashMovementsDto> ComputeAsync(DateOnly from, DateOnly to, Guid? entity, CancellationToken ct)
    {
        await currencies.EnsureSingleCurrencyAsync(entity, ct);
        var cash = await accounts.ListActiveCashAccountsAsync(ct);
        var ids = cash.Select(c => c.Id).ToList();
        var moves = await journals.ListPostedCashAccountLinesAsync(ids, to, entity, ct);
        var rows = cash.Select(c =>
        {
            var mine = moves.Where(m => m.AccountId == c.Id).ToList();
            var opening = mine.Where(m => m.Date < from).Sum(m => m.Debit - m.Credit);
            var within = mine.Where(m => m.Date >= from).ToList();
            var inflow = within.Sum(m => m.Debit); var outflow = within.Sum(m => m.Credit);
            return new CashMovementRow(c.Id, c.Code, c.Name, opening, inflow, outflow, opening + inflow - outflow);
        }).Where(r => r.Opening != 0 || r.Inflows != 0 || r.Outflows != 0).ToList();
        var bySource = moves.Where(m => m.Date >= from).GroupBy(m => m.SourceType).Select(g => new CashSourceRow(g.Key, g.Sum(m => m.Debit), g.Sum(m => m.Credit))).OrderBy(s => s.Source).ToList();
        return new CashMovementsDto(from, to, rows, bySource, rows.Sum(r => r.Opening), rows.Sum(r => r.Inflows), rows.Sum(r => r.Outflows), rows.Sum(r => r.Closing));
    }
}
