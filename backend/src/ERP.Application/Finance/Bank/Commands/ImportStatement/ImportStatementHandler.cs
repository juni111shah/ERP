using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class ImportStatementHandler(IBankStatementLineRepository lines, ILegalEntityGuard guard, CashAccountGuard cashAccounts) : ICommandHandler<ImportStatementCommand, ImportResult>
{
    public async Task<ImportResult> Handle(ImportStatementCommand command, CancellationToken ct)
    {
        // 1. The legal entity and an active bank or cash account.
        await guard.RequireAsync(command.LegalEntityId, ct);
        await cashAccounts.RequireAsync(command.AccountId, "accountId", ct);

        // 2. Keep the lines that are not stored yet.
        var existing = await lines.ListKeysAsync(command.AccountId, command.LegalEntityId, ct);
        var seen = existing.Select(e => (e.Date, e.Amount, e.Reference ?? "", e.Description)).ToList();
        var fresh = new List<BankStatementLine>();
        var duplicates = 0;
        foreach (var l in command.Lines)
        {
            var key = (l.Date, l.Amount, l.Reference?.Trim() ?? "", l.Description.Trim());
            // The same line twice in one file is legitimate (two identical fees); only lines that are already stored are duplicates.
            if (seen.Remove(key)) { duplicates++; continue; }
            fresh.Add(new BankStatementLine { LegalEntityId = command.LegalEntityId, AccountId = command.AccountId, Date = l.Date, Description = l.Description.Trim(), Reference = l.Reference?.Trim(), Amount = l.Amount, Status = StatementLineStatus.Unmatched });
        }

        // 3. Save them.
        lines.AddRange(fresh);
        await lines.SaveChangesAsync(ct);
        return new ImportResult(fresh.Count, duplicates);
    }
}
