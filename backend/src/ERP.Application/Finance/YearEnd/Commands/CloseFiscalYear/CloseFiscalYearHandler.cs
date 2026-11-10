using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;
using ERP.Domain.Organizations;

namespace ERP.Application.Finance;

internal sealed class CloseFiscalYearHandler(
    IFiscalYearCloseRepository closes, IJournalEntryRepository journals, IAccountRepository accounts, IRepository<LegalEntity> entities, YearEndScope scope,
    IPostingService posting, ISettingsService settings, ICurrentUser user, IClock clock, IUnitOfWork uow)
    : ICommandHandler<CloseFiscalYearCommand, FiscalYearCloseDto>
{
    public Task<FiscalYearCloseDto> Handle(CloseFiscalYearCommand command, CancellationToken ct) => uow.InTransactionAsync(token => CloseAsync(command, token), ct);

    private async Task<FiscalYearCloseDto> CloseAsync(CloseFiscalYearCommand cmd, CancellationToken ct)
    {
        // 1. A fiscal year of this entity that has ended.
        await scope.RequireEntityAsync(cmd.LegalEntityId, ct);
        var startMonth = await settings.GetAsync<int?>(FiscalYears.StartMonthSetting, cmd.LegalEntityId, ct) ?? 1;
        var (start, end) = FiscalYears.Range(cmd.FiscalYear, startMonth);
        if (end > DateOnly.FromDateTime(clock.UtcNow)) throw new FieldValidationException("fiscalYear", $"Fiscal year {cmd.FiscalYear} runs to {end:yyyy-MM-dd} and has not ended yet.");

        // 2. Already closed: nothing changes. A later closed year blocks it.
        var existing = await closes.FindLiveAsync(cmd.LegalEntityId, cmd.FiscalYear, ct);
        if (existing is not null) return FiscalYearCloseDto.From(existing, alreadyClosed: true);
        if (await closes.AnyAsync(c => c.LegalEntityId == cmd.LegalEntityId && c.ReopenedAt == null && c.YearEnd > end, ct))
            throw new ConflictException("A later fiscal year is already closed. Reopen it first.");

        // 3. The closing entry belongs on the year end even when that date is already locked. The lock is restored below, in the same transaction.
        var entity = await entities.GetForUpdateAsync(cmd.LegalEntityId, ct);
        var previousLock = entity.BooksLockedTo;
        if (previousLock is { } locked && locked >= end)
        {
            entity.BooksLockedTo = null;
            await uow.SaveChangesAsync(ct);
        }

        // 4. Post the closing entry (none when nothing has a balance).
        var (lines, net) = await ClosingLinesAsync(cmd.LegalEntityId, end, cmd.FiscalYear, ct);
        JournalEntry? journal = null;
        if (lines.Count > 0)
            journal = await posting.PostAsync(new PostingRequest(cmd.LegalEntityId, end, LedgerSources.YearEnd, null, $"Year-end close {cmd.FiscalYear}", $"FY{cmd.FiscalYear}", lines), ct);

        // 5. Lock the books to the year end (or keep a later lock), record the close and save.
        entity.BooksLockedTo = previousLock is { } later && later > end ? later : end;
        var close = new FiscalYearClose
        {
            LegalEntityId = cmd.LegalEntityId, FiscalYear = cmd.FiscalYear, YearStart = start, YearEnd = end, JournalEntryId = journal?.Id, NetResult = net,
            PreviousBooksLockedTo = previousLock, ClosedAt = clock.UtcNow, ClosedBy = user.UserId,
        };
        closes.Add(close);
        await uow.SaveChangesAsync(ct);
        return FiscalYearCloseDto.From(close);
    }

    /// <summary>One line per income or expense account with a balance, and retained earnings for the difference (nothing when the balances already net to zero).</summary>
    private async Task<(List<PostingLine> Lines, decimal Net)> ClosingLinesAsync(Guid legalEntityId, DateOnly end, int fiscalYear, CancellationToken ct)
    {
        var profitAndLoss = await accounts.ListIncomeAndExpenseIdsByCodeAsync(ct);
        var movements = await journals.ListPostedMovementsOfAccountsAsync(legalEntityId, end, profitAndLoss, ct);
        var balances = movements.GroupBy(m => m.AccountId).ToDictionary(g => g.Key, g => g.Sum(m => m.Debit - m.Credit));
        var memo = $"Year-end close {fiscalYear}";
        var lines = new List<PostingLine>();
        foreach (var id in profitAndLoss)
        {
            var balance = balances.GetValueOrDefault(id);
            if (balance > 0) lines.Add(new PostingLine(id, 0, balance, memo));
            else if (balance < 0) lines.Add(new PostingLine(id, -balance, 0, memo));
        }
        var net = -balances.Values.Sum(); // Dr - Cr of income and expense: a credit balance is a profit
        if (net != 0)
        {
            var retained = await posting.AccountForRoleAsync(SystemRoles.RetainedEarnings, ct);
            lines.Add(net > 0 ? new PostingLine(retained, 0, net, memo) : new PostingLine(retained, -net, 0, memo));
        }
        return (lines, net);
    }
}
