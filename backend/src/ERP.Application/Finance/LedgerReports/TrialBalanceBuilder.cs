namespace ERP.Application.Finance;

/// <summary>Opening balance, movement and closing balance per account for a period: the base of the trial balance, income statement and balance sheet. Draft journals never count.</summary>
public sealed class TrialBalanceBuilder(IJournalEntryRepository journals, IAccountRepository accounts, IReportCurrencyGuard currencies)
{
    public async Task<List<TrialBalanceRow>> BuildAsync(DateOnly? from, DateOnly to, Guid? entityId, CancellationToken ct, bool excludeYearEnd = false)
    {
        await currencies.EnsureSingleCurrencyAsync(entityId, ct);
        // Sum on the client: decimal SUM is not portable across the supported databases and the projection is only three columns.
        var movements = await journals.ListPostedMovementsAsync(to, entityId, excludeYearEnd, ct);
        var byAccount = movements.GroupBy(m => m.AccountId).ToDictionary(g => g.Key, g => g.ToList());
        var list = await accounts.ListReportRowsByCodeAsync(ct);
        var result = new List<TrialBalanceRow>();
        foreach (var a in list)
        {
            if (!byAccount.TryGetValue(a.Id, out var ms)) continue;
            var before = from is { } f ? ms.Where(m => m.Date < f) : Enumerable.Empty<LedgerMovement>();
            var within = from is { } f2 ? ms.Where(m => m.Date >= f2) : ms;
            var opening = before.Sum(m => m.Debit - m.Credit);
            var debit = within.Sum(m => m.Debit); var credit = within.Sum(m => m.Credit);
            if (opening == 0 && debit == 0 && credit == 0) continue;
            result.Add(new TrialBalanceRow(a.Id, a.Code, a.Name, a.Type, a.ReportingGroup, opening, debit, credit, opening + debit - credit));
        }
        return result;
    }
}
