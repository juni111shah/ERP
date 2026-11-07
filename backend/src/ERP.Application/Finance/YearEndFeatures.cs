using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Domain.Finance;
using ERP.Domain.Organizations;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Finance;

public sealed record FiscalYearCloseDto(
    Guid Id, Guid LegalEntityId, int FiscalYear, DateOnly YearStart, DateOnly YearEnd, Guid? JournalEntryId, decimal NetResult, DateOnly? PreviousBooksLockedTo,
    DateTime ClosedAt, Guid? ClosedBy, DateTime? ReopenedAt, Guid? ReopenedBy, string? ReopenReason, Guid? ReversalJournalId, bool AlreadyClosed = false);

/// <summary>
/// Closes a fiscal year: moves every income and expense balance to the retained earnings account in one journal dated the last day of the
/// year and locks the books to that date. Running it again for a closed year changes nothing and returns the existing close.
/// Earlier years that were never closed are swept into the same entry, so the first close of a long-running workspace needs no replay.
/// </summary>
public sealed record CloseFiscalYear(Guid LegalEntityId, int FiscalYear) : ICommand<FiscalYearCloseDto>, IRetryOnConcurrency, IRequirePermission
{
    public string RequiredPermission => CorePermissions.YearEndClose;
}

/// <summary>Reopens the latest closed year: reverses the closing entry on the same date and moves the books lock back. The reason is required.</summary>
public sealed record ReopenFiscalYear(Guid LegalEntityId, int FiscalYear, string? Reason) : ICommand<FiscalYearCloseDto>, IRetryOnConcurrency, IRequirePermission
{
    public string RequiredPermission => CorePermissions.YearEndReopen;
}

public sealed class CloseFiscalYearValidator : AbstractValidator<CloseFiscalYear>
{
    public CloseFiscalYearValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.FiscalYear).InclusiveBetween(2000, 2100).WithMessage("Enter the fiscal year, for example 2026.");
    }
}

public sealed class ReopenFiscalYearValidator : AbstractValidator<ReopenFiscalYear>
{
    public ReopenFiscalYearValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.FiscalYear).InclusiveBetween(2000, 2100).WithMessage("Enter the fiscal year, for example 2026.");
        RuleFor(x => x.Reason).ValidVoidReason();
    }
}

/// <summary>The fiscal calendar (pure). A fiscal year is named for the calendar year it ends in: with a start month of 4, year 2026 runs 1 Apr 2025 to 31 Mar 2026.</summary>
public static class FiscalYears
{
    public const string StartMonthSetting = "core.fiscalYearStartMonth";

    public static (DateOnly Start, DateOnly End) Range(int fiscalYear, int startMonth)
    {
        var month = startMonth is >= 1 and <= 12 ? startMonth : 1;
        var start = new DateOnly(month == 1 ? fiscalYear : fiscalYear - 1, month, 1);
        return (start, start.AddYears(1).AddDays(-1));
    }

    /// <summary>First day of the fiscal year that contains the date.</summary>
    public static DateOnly StartOf(DateOnly date, int startMonth)
    {
        var month = startMonth is >= 1 and <= 12 ? startMonth : 1;
        return new DateOnly(date.Month >= month ? date.Year : date.Year - 1, month, 1);
    }
}

internal sealed class YearEndHandlers(
    IRepository<FiscalYearClose> closes, IRepository<JournalEntry> journals, IReadRepository<Account> accounts, IRepository<LegalEntity> entities, ILegalEntityGuard guard,
    IPostingService posting, ISettingsService settings, ICurrentUser user, IClock clock, IUnitOfWork uow) :
    IRequestHandler<CloseFiscalYear, FiscalYearCloseDto>,
    IRequestHandler<ReopenFiscalYear, FiscalYearCloseDto>
{
    public Task<FiscalYearCloseDto> Handle(CloseFiscalYear cmd, CancellationToken ct) => uow.InTransactionAsync(token => CloseCoreAsync(cmd, token), ct);

    public Task<FiscalYearCloseDto> Handle(ReopenFiscalYear cmd, CancellationToken ct) => uow.InTransactionAsync(token => ReopenCoreAsync(cmd, token), ct);

    private async Task<FiscalYearCloseDto> CloseCoreAsync(CloseFiscalYear cmd, CancellationToken ct)
    {
        await RequireEntityAsync(cmd.LegalEntityId, ct);
        var startMonth = await settings.GetAsync<int?>(FiscalYears.StartMonthSetting, cmd.LegalEntityId, ct) ?? 1;
        var (start, end) = FiscalYears.Range(cmd.FiscalYear, startMonth);
        if (end > DateOnly.FromDateTime(clock.UtcNow)) throw new FieldValidationException("fiscalYear", $"Fiscal year {cmd.FiscalYear} runs to {end:yyyy-MM-dd} and has not ended yet.");

        var existing = await LiveCloseAsync(cmd.LegalEntityId, cmd.FiscalYear, tracked: false, ct);
        if (existing is not null) return ToDto(existing, alreadyClosed: true);
        if (await closes.AnyAsync(c => c.LegalEntityId == cmd.LegalEntityId && c.ReopenedAt == null && c.YearEnd > end, ct))
            throw new ConflictException("A later fiscal year is already closed. Reopen it first.");

        var entity = await entities.GetForUpdateAsync(cmd.LegalEntityId, ct);
        var previousLock = entity.BooksLockedTo;
        // The closing entry belongs on the year end even when that date is already locked. The lock is restored below, in the same transaction.
        if (previousLock is { } locked && locked >= end)
        {
            entity.BooksLockedTo = null;
            await uow.SaveChangesAsync(ct);
        }

        var (lines, net) = await ClosingLinesAsync(cmd.LegalEntityId, end, cmd.FiscalYear, ct);
        JournalEntry? journal = null;
        if (lines.Count > 0)
            journal = await posting.PostAsync(new PostingRequest(cmd.LegalEntityId, end, LedgerSources.YearEnd, null, $"Year-end close {cmd.FiscalYear}", $"FY{cmd.FiscalYear}", lines), ct);

        entity.BooksLockedTo = previousLock is { } later && later > end ? later : end;
        var close = new FiscalYearClose
        {
            LegalEntityId = cmd.LegalEntityId, FiscalYear = cmd.FiscalYear, YearStart = start, YearEnd = end, JournalEntryId = journal?.Id, NetResult = net,
            PreviousBooksLockedTo = previousLock, ClosedAt = clock.UtcNow, ClosedBy = user.UserId,
        };
        closes.Add(close);
        await uow.SaveChangesAsync(ct);
        return ToDto(close);
    }

    private async Task<FiscalYearCloseDto> ReopenCoreAsync(ReopenFiscalYear cmd, CancellationToken ct)
    {
        await RequireEntityAsync(cmd.LegalEntityId, ct);
        var close = await LiveCloseAsync(cmd.LegalEntityId, cmd.FiscalYear, tracked: true, ct) ?? throw new ConflictException($"Fiscal year {cmd.FiscalYear} is not closed.");
        if (await closes.AnyAsync(c => c.LegalEntityId == cmd.LegalEntityId && c.ReopenedAt == null && c.YearEnd > close.YearEnd, ct))
            throw new ConflictException("A later fiscal year is closed. Reopen the latest year first.");

        var entity = await entities.GetForUpdateAsync(cmd.LegalEntityId, ct);
        entity.BooksLockedTo = null;
        await uow.SaveChangesAsync(ct); // the reversal is dated on the year end, so the lock has to be out of the way first

        var reason = cmd.Reason!.Trim();
        if (close.JournalEntryId is { } journalId)
        {
            var journal = await journals.Tracked(new Specification<JournalEntry>(j => j.Id == journalId).Include(j => j.Lines)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
            var memo = $"Reopen fiscal year {cmd.FiscalYear}: {reason}";
            var reversal = await posting.ReverseAsync(journal, close.YearEnd, memo.Length <= VoidMemo.MaxMemo ? memo : memo[..VoidMemo.MaxMemo], ct);
            reversal.SourceType = LedgerSources.YearEnd; // stays out of income statements and budgets, as the entry it undoes does
            close.ReversalJournalId = reversal.Id;
        }

        // Back to the lock from before the close, unless that lock still covers the year being reopened.
        entity.BooksLockedTo = close.PreviousBooksLockedTo is { } previous && previous >= close.YearEnd ? close.YearStart.AddDays(-1) : close.PreviousBooksLockedTo;
        close.ReopenedAt = clock.UtcNow;
        close.ReopenedBy = user.UserId;
        close.ReopenReason = reason;
        await uow.SaveChangesAsync(ct);
        return ToDto(close);
    }

    /// <summary>A selected working entity narrows every journal query, so closing another one would read no balances and post nothing.</summary>
    private async Task RequireEntityAsync(Guid legalEntityId, CancellationToken ct)
    {
        await guard.RequireAsync(legalEntityId, ct);
        if (user.LegalEntityId is { } selected && selected != legalEntityId)
            throw new FieldValidationException("legalEntityId", "Switch to this legal entity, or to all entities, to close its year.");
    }

    private Task<FiscalYearClose?> LiveCloseAsync(Guid legalEntityId, int fiscalYear, bool tracked, CancellationToken ct)
    {
        var spec = new Specification<FiscalYearClose>(c => c.LegalEntityId == legalEntityId && c.FiscalYear == fiscalYear && c.ReopenedAt == null);
        return (tracked ? closes.Tracked(spec) : closes.Query(spec)).FirstOrDefaultAsync(ct);
    }

    /// <summary>One line per income or expense account with a balance, and retained earnings for the difference (nothing when the balances already net to zero).</summary>
    private async Task<(List<PostingLine> Lines, decimal Net)> ClosingLinesAsync(Guid legalEntityId, DateOnly end, int fiscalYear, CancellationToken ct)
    {
        var profitAndLoss = await accounts.Query(new Specification<Account>(a => a.Type == AccountType.Income || a.Type == AccountType.Expense)).OrderBy(a => a.Code).Select(a => a.Id).ToListAsync(ct);
        var movements = await journals.Query(new Specification<JournalEntry>(j => j.Status != JournalStatus.Draft && j.LegalEntityId == legalEntityId && j.Date <= end))
            .SelectMany(j => j.Lines.Where(l => profitAndLoss.Contains(l.AccountId)), (j, l) => new { l.AccountId, l.Debit, l.Credit }).ToListAsync(ct);
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

    private static FiscalYearCloseDto ToDto(FiscalYearClose c, bool alreadyClosed = false) => new(
        c.Id, c.LegalEntityId, c.FiscalYear, c.YearStart, c.YearEnd, c.JournalEntryId, c.NetResult, c.PreviousBooksLockedTo,
        c.ClosedAt, c.ClosedBy, c.ReopenedAt, c.ReopenedBy, c.ReopenReason, c.ReversalJournalId, alreadyClosed);
}
