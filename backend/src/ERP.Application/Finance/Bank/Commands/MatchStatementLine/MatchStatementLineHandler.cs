using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class MatchStatementLineHandler(IBankStatementLineRepository lines, BankLedger ledger) : ICommandHandler<MatchStatementLineCommand, StatementLineDto>
{
    public async Task<StatementLineDto> Handle(MatchStatementLineCommand command, CancellationToken ct)
    {
        // 1. Neither the statement line nor the ledger line may be matched already.
        var l = await lines.GetForUpdateAsync(command.LineId, ct);
        if (l.Status == StatementLineStatus.Matched) throw new ConflictException("This statement line is already matched.");
        if (await lines.AnyAsync(x => x.MatchedJournalLineId == command.JournalLineId, ct)) throw new ConflictException("That ledger line is already matched to another statement line.");

        // 2. The ledger line must be on this bank account, for the same amount.
        var unmatched = await ledger.UnmatchedAsync(l.AccountId, l.LegalEntityId, DateOnly.MaxValue, ct);
        var target = unmatched.FirstOrDefault(x => x.JournalLineId == command.JournalLineId) ?? throw new FieldValidationException("journalLineId", "That ledger line is not on this bank account.");
        if (target.Amount != l.Amount) throw new FieldValidationException("journalLineId", $"The amounts differ (statement {l.Amount:0.00}, ledger {target.Amount:0.00}).");

        // 3. Match and save.
        l.MatchedJournalLineId = target.JournalLineId; l.Status = StatementLineStatus.Matched;
        await lines.SaveChangesAsync(ct);
        return StatementLineDto.From(l);
    }
}
