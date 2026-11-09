namespace ERP.Application.Finance;

/// <summary>The ledger side of a bank reconciliation; shared by auto-match, match and the reconciliation report.</summary>
public sealed class BankLedger(IJournalEntryRepository journals, IBankStatementLineRepository lines)
{
    /// <summary>The account's ledger lines up to <paramref name="asOf"/> that no statement line is matched to.</summary>
    public async Task<List<LedgerItem>> UnmatchedAsync(Guid accountId, Guid entityId, DateOnly asOf, CancellationToken ct)
    {
        var all = await journals.ListAccountLinesAsync(accountId, entityId, asOf, ct);
        var matched = await lines.MatchedJournalLineIdsAsync(accountId, ct);
        return all.Where(x => !matched.Contains(x.JournalLineId)).Select(ToItem).ToList();
    }

    public static LedgerItem ToItem(AccountLedgerLine r) => new(r.JournalLineId, r.JournalId, r.Number, r.Date, r.Description, r.Debit - r.Credit);
}
