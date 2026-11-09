using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class BookStatementLineHandler(
    IBankStatementLineRepository lines,
    IReadRepository<JournalEntry> journals,
    IReadRepository<Account> accounts,
    IPostingService posting,
    IUnitOfWork uow)
    : ICommandHandler<BookStatementLineCommand, StatementLineDto>
{
    public Task<StatementLineDto> Handle(BookStatementLineCommand command, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. Only an unmatched line that was never booked before.
        var l = await lines.GetForUpdateAsync(command.LineId, token);
        if (l.Status == StatementLineStatus.Matched) throw new ConflictException("This statement line is already matched.");
        // Unmatching a booked line leaves its journal behind; booking again would double the entry, so match the line to that journal instead.
        if (await journals.AnyAsync(j => j.SourceType == "bank" && j.SourceId == l.Id && j.Status == JournalStatus.Posted, token))
            throw new ConflictException("This line was already booked from the statement. Match it to its ledger entry instead of booking it again.");

        // 2. Another active, postable account.
        var counter = await accounts.FindAsync(command.CounterAccountId, token);
        if (counter is null || !counter.IsPostable || !counter.IsActive || counter.Id == l.AccountId) throw new FieldValidationException("counterAccountId", "Choose a different active, postable account.");

        // 3. Post the line against it and match the line to the bank side of that journal.
        var memo = command.Memo?.Trim() is { Length: > 0 } m ? m : l.Description;
        var amount = Math.Abs(l.Amount);
        var bankLine = l.Amount > 0 ? new PostingLine(l.AccountId, amount, 0, memo) : new PostingLine(l.AccountId, 0, amount, memo);
        var counterLine = l.Amount > 0 ? new PostingLine(counter.Id, 0, amount, memo) : new PostingLine(counter.Id, amount, 0, memo);
        var journal = await posting.PostAsync(new PostingRequest(l.LegalEntityId, l.Date, "bank", l.Id, memo, l.Reference, [bankLine, counterLine]), token);
        l.MatchedJournalLineId = journal.Lines.First(x => x.AccountId == l.AccountId).Id; l.Status = StatementLineStatus.Matched;
        await lines.SaveChangesAsync(token);
        return StatementLineDto.From(l);
    }, ct);
}
