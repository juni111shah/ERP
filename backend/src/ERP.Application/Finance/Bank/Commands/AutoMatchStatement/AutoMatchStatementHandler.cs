using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class AutoMatchStatementHandler(IBankStatementLineRepository lines, BankLedger ledger, ILegalEntityGuard guard) : ICommandHandler<AutoMatchStatementCommand, int>
{
    private const int MatchWindowDays = 5;

    public async Task<int> Handle(AutoMatchStatementCommand command, CancellationToken ct)
    {
        // 1. The unmatched statement lines, oldest first.
        await guard.RequireAsync(command.LegalEntityId, ct);
        var open = await lines.ListUnmatchedForUpdateAsync(command.AccountId, command.LegalEntityId, ct);
        if (open.Count == 0) return 0;

        // 2. Match each to the closest unmatched ledger line with the same amount within the window.
        var unmatched = await ledger.UnmatchedAsync(command.AccountId, command.LegalEntityId, open.Max(l => l.Date).AddDays(MatchWindowDays), ct);
        var matched = 0;
        foreach (var l in open)
        {
            var hit = unmatched.Where(x => x.Amount == l.Amount && Math.Abs(x.Date.DayNumber - l.Date.DayNumber) <= MatchWindowDays).OrderBy(x => Math.Abs(x.Date.DayNumber - l.Date.DayNumber)).FirstOrDefault();
            if (hit is null) continue;
            l.MatchedJournalLineId = hit.JournalLineId; l.Status = StatementLineStatus.Matched;
            unmatched.Remove(hit);
            matched++;
        }

        // 3. Save.
        await lines.SaveChangesAsync(ct);
        return matched;
    }
}
