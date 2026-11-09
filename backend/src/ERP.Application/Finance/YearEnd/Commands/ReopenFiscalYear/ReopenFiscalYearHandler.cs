using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Organizations;

namespace ERP.Application.Finance;

internal sealed class ReopenFiscalYearHandler(
    IFiscalYearCloseRepository closes, IJournalEntryRepository journals, IRepository<LegalEntity> entities, YearEndScope scope,
    IPostingService posting, ICurrentUser user, IClock clock, IUnitOfWork uow)
    : ICommandHandler<ReopenFiscalYearCommand, FiscalYearCloseDto>
{
    public Task<FiscalYearCloseDto> Handle(ReopenFiscalYearCommand command, CancellationToken ct) => uow.InTransactionAsync(token => ReopenAsync(command, token), ct);

    private async Task<FiscalYearCloseDto> ReopenAsync(ReopenFiscalYearCommand cmd, CancellationToken ct)
    {
        // 1. The latest closed year of this entity.
        await scope.RequireEntityAsync(cmd.LegalEntityId, ct);
        var close = await closes.FindLiveForUpdateAsync(cmd.LegalEntityId, cmd.FiscalYear, ct) ?? throw new ConflictException($"Fiscal year {cmd.FiscalYear} is not closed.");
        if (await closes.AnyAsync(c => c.LegalEntityId == cmd.LegalEntityId && c.ReopenedAt == null && c.YearEnd > close.YearEnd, ct))
            throw new ConflictException("A later fiscal year is closed. Reopen the latest year first.");

        // 2. Lift the lock.
        var entity = await entities.GetForUpdateAsync(cmd.LegalEntityId, ct);
        entity.BooksLockedTo = null;
        await uow.SaveChangesAsync(ct); // the reversal is dated on the year end, so the lock has to be out of the way first

        // 3. Reverse the closing entry on the year end.
        var reason = cmd.Reason!.Trim();
        if (close.JournalEntryId is { } journalId)
        {
            var journal = await journals.GetWithLinesForUpdateAsync(journalId, ct);
            var memo = $"Reopen fiscal year {cmd.FiscalYear}: {reason}";
            var reversal = await posting.ReverseAsync(journal, close.YearEnd, memo.Length <= VoidMemo.MaxMemo ? memo : memo[..VoidMemo.MaxMemo], ct);
            reversal.SourceType = LedgerSources.YearEnd; // stays out of income statements and budgets, as the entry it undoes does
            close.ReversalJournalId = reversal.Id;
        }

        // 4. Back to the lock from before the close, unless that lock still covers the year being reopened.
        entity.BooksLockedTo = close.PreviousBooksLockedTo is { } previous && previous >= close.YearEnd ? close.YearStart.AddDays(-1) : close.PreviousBooksLockedTo;
        close.ReopenedAt = clock.UtcNow;
        close.ReopenedBy = user.UserId;
        close.ReopenReason = reason;
        await uow.SaveChangesAsync(ct);
        return FiscalYearCloseDto.From(close);
    }
}
