using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Finance;

public sealed record StatementLineInput(DateOnly Date, string Description, string? Reference, decimal Amount);
public sealed record ImportResult(int Imported, int Duplicates);
public sealed record StatementLineDto(Guid Id, Guid AccountId, DateOnly Date, string Description, string? Reference, decimal Amount, StatementLineStatus Status, Guid? MatchedJournalLineId);
public sealed record LedgerItem(Guid JournalLineId, Guid JournalId, string Number, DateOnly Date, string? Description, decimal Amount);
public sealed record ReconciliationDto(
    Guid AccountId, string Code, string Name, DateOnly AsOf, decimal LedgerBalance, int Matched, IReadOnlyList<StatementLineDto> UnmatchedStatement, IReadOnlyList<LedgerItem> UnmatchedLedger,
    decimal UnmatchedStatementTotal, decimal UnmatchedLedgerTotal, bool Reconciled);
public sealed record CashPositionRow(Guid AccountId, string Code, string Name, bool IsBank, decimal Balance);
public sealed record CashPositionDto(DateOnly AsOf, IReadOnlyList<CashPositionRow> Accounts, decimal Total);

/// <summary>Adds bank statement lines to a bank account. Lines already imported (same date, amount, reference and text) are skipped.</summary>
public sealed record ImportStatement(Guid LegalEntityId, Guid AccountId, List<StatementLineInput> Lines) : ICommand<ImportResult>, IRequirePermission { public string RequiredPermission => CorePermissions.BankWrite; }
/// <summary>Matches unmatched statement lines to ledger lines with the same amount within five days.</summary>
public sealed record AutoMatchStatement(Guid LegalEntityId, Guid AccountId) : ICommand<int>, IRequirePermission { public string RequiredPermission => CorePermissions.BankWrite; }
public sealed record MatchStatementLine(Guid LineId, Guid JournalLineId) : ICommand<StatementLineDto>, IRequirePermission { public string RequiredPermission => CorePermissions.BankWrite; }
public sealed record UnmatchStatementLine(Guid LineId) : ICommand<StatementLineDto>, IRequirePermission { public string RequiredPermission => CorePermissions.BankWrite; }
/// <summary>Books an unmatched statement line (a bank charge, interest...) against another account and matches it.</summary>
public sealed record BookStatementLine(Guid LineId, Guid CounterAccountId, string? Memo) : ICommand<StatementLineDto>, IRequirePermission { public string RequiredPermission => CorePermissions.BankWrite; }
public sealed record TransferFunds(Guid LegalEntityId, Guid FromAccountId, Guid ToAccountId, decimal Amount, DateOnly Date, string? Memo) : ICommand<Guid>, IRequirePermission { public string RequiredPermission => CorePermissions.BankWrite; }
public sealed record GetReconciliation(Guid AccountId, Guid? LegalEntityId, DateOnly AsOf) : IQuery<ReconciliationDto>, IRequirePermission { public string RequiredPermission => CorePermissions.BankRead; }
public sealed record GetCashPosition(DateOnly AsOf, Guid? LegalEntityId) : IQuery<CashPositionDto>, IRequirePermission { public string RequiredPermission => CorePermissions.BankRead; }

public sealed class ImportStatementValidator : AbstractValidator<ImportStatement>
{
    public ImportStatementValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.Lines).NotNull().Must(l => l is { Count: > 0 and <= 2000 }).WithMessage("Import between 1 and 2000 lines at a time.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.Description).NotEmpty().MaximumLength(300);
            l.RuleFor(x => x.Reference).MaximumLength(100);
            l.RuleFor(x => x.Amount).NotEqual(0);
        });
    }
}
public sealed class TransferFundsValidator : AbstractValidator<TransferFunds>
{
    public TransferFundsValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x).Must(x => x.FromAccountId != x.ToAccountId).WithName("toAccountId").WithMessage("Choose two different accounts.");
    }
}

internal sealed class BankHandlers(
    IRepository<BankStatementLine> lines, IReadRepository<JournalEntry> journals, IReadRepository<Account> accounts, ILegalEntityGuard guard, IPostingService posting, IUnitOfWork uow) :
    IRequestHandler<ImportStatement, ImportResult>,
    IRequestHandler<AutoMatchStatement, int>,
    IRequestHandler<MatchStatementLine, StatementLineDto>,
    IRequestHandler<UnmatchStatementLine, StatementLineDto>,
    IRequestHandler<BookStatementLine, StatementLineDto>,
    IRequestHandler<TransferFunds, Guid>,
    IRequestHandler<GetReconciliation, ReconciliationDto>,
    IRequestHandler<GetCashPosition, CashPositionDto>
{
    private const int MatchWindowDays = 5;
    private sealed record LedgerRow(Guid JournalLineId, Guid JournalId, string Number, DateOnly Date, string? Description, decimal Debit, decimal Credit);

    public async Task<ImportResult> Handle(ImportStatement cmd, CancellationToken ct)
    {
        await guard.RequireAsync(cmd.LegalEntityId, ct);
        await RequireCashAccountAsync(cmd.AccountId, "accountId", ct);
        var existing = await lines.Query(new Specification<BankStatementLine>(l => l.AccountId == cmd.AccountId && l.LegalEntityId == cmd.LegalEntityId))
            .Select(l => new { l.Date, l.Amount, l.Reference, l.Description }).ToListAsync(ct);
        var seen = existing.Select(e => (e.Date, e.Amount, e.Reference ?? "", e.Description)).ToList();
        var fresh = new List<BankStatementLine>();
        var duplicates = 0;
        foreach (var l in cmd.Lines)
        {
            var key = (l.Date, l.Amount, l.Reference?.Trim() ?? "", l.Description.Trim());
            // The same line twice in one file is legitimate (two identical fees); only lines that are already stored are duplicates.
            if (seen.Remove(key)) { duplicates++; continue; }
            fresh.Add(new BankStatementLine { LegalEntityId = cmd.LegalEntityId, AccountId = cmd.AccountId, Date = l.Date, Description = l.Description.Trim(), Reference = l.Reference?.Trim(), Amount = l.Amount, Status = StatementLineStatus.Unmatched });
        }
        lines.AddRange(fresh);
        await lines.SaveChangesAsync(ct);
        return new ImportResult(fresh.Count, duplicates);
    }

    public async Task<int> Handle(AutoMatchStatement cmd, CancellationToken ct)
    {
        await guard.RequireAsync(cmd.LegalEntityId, ct);
        var open = await lines.Tracked(new Specification<BankStatementLine>(l => l.AccountId == cmd.AccountId && l.LegalEntityId == cmd.LegalEntityId && l.Status == StatementLineStatus.Unmatched)).OrderBy(l => l.Date).ToListAsync(ct);
        if (open.Count == 0) return 0;
        var ledger = await UnmatchedLedgerAsync(cmd.AccountId, cmd.LegalEntityId, open.Max(l => l.Date).AddDays(MatchWindowDays), ct);
        var matched = 0;
        foreach (var l in open)
        {
            var hit = ledger.Where(x => x.Amount == l.Amount && Math.Abs(x.Date.DayNumber - l.Date.DayNumber) <= MatchWindowDays).OrderBy(x => Math.Abs(x.Date.DayNumber - l.Date.DayNumber)).FirstOrDefault();
            if (hit is null) continue;
            l.MatchedJournalLineId = hit.JournalLineId; l.Status = StatementLineStatus.Matched;
            ledger.Remove(hit);
            matched++;
        }
        await lines.SaveChangesAsync(ct);
        return matched;
    }

    public async Task<StatementLineDto> Handle(MatchStatementLine cmd, CancellationToken ct)
    {
        var l = await lines.GetForUpdateAsync(cmd.LineId, ct);
        if (l.Status == StatementLineStatus.Matched) throw new ConflictException("This statement line is already matched.");
        if (await lines.AnyAsync(x => x.MatchedJournalLineId == cmd.JournalLineId, ct)) throw new ConflictException("That ledger line is already matched to another statement line.");
        var ledger = await UnmatchedLedgerAsync(l.AccountId, l.LegalEntityId, DateOnly.MaxValue, ct);
        var target = ledger.FirstOrDefault(x => x.JournalLineId == cmd.JournalLineId) ?? throw new FieldValidationException("journalLineId", "That ledger line is not on this bank account.");
        if (target.Amount != l.Amount) throw new FieldValidationException("journalLineId", $"The amounts differ (statement {l.Amount:0.00}, ledger {target.Amount:0.00}).");
        l.MatchedJournalLineId = target.JournalLineId; l.Status = StatementLineStatus.Matched;
        await lines.SaveChangesAsync(ct);
        return ToDto(l);
    }

    public async Task<StatementLineDto> Handle(UnmatchStatementLine cmd, CancellationToken ct)
    {
        var l = await lines.GetForUpdateAsync(cmd.LineId, ct);
        l.MatchedJournalLineId = null; l.Status = StatementLineStatus.Unmatched;
        await lines.SaveChangesAsync(ct);
        return ToDto(l);
    }

    public Task<StatementLineDto> Handle(BookStatementLine cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var l = await lines.GetForUpdateAsync(cmd.LineId, token);
        if (l.Status == StatementLineStatus.Matched) throw new ConflictException("This statement line is already matched.");
        var counter = await accounts.FindAsync(cmd.CounterAccountId, token);
        if (counter is null || !counter.IsPostable || !counter.IsActive || counter.Id == l.AccountId) throw new FieldValidationException("counterAccountId", "Choose a different active, postable account.");
        var memo = cmd.Memo?.Trim() is { Length: > 0 } m ? m : l.Description;
        var amount = Math.Abs(l.Amount);
        var bankLine = l.Amount > 0 ? new PostingLine(l.AccountId, amount, 0, memo) : new PostingLine(l.AccountId, 0, amount, memo);
        var counterLine = l.Amount > 0 ? new PostingLine(counter.Id, 0, amount, memo) : new PostingLine(counter.Id, amount, 0, memo);
        var journal = await posting.PostAsync(new PostingRequest(l.LegalEntityId, l.Date, "bank", l.Id, memo, l.Reference, [bankLine, counterLine]), token);
        l.MatchedJournalLineId = journal.Lines.First(x => x.AccountId == l.AccountId).Id; l.Status = StatementLineStatus.Matched;
        await lines.SaveChangesAsync(token);
        return ToDto(l);
    }, ct);

    public async Task<Guid> Handle(TransferFunds cmd, CancellationToken ct)
    {
        await RequireCashAccountAsync(cmd.FromAccountId, "fromAccountId", ct);
        await RequireCashAccountAsync(cmd.ToAccountId, "toAccountId", ct);
        var memo = cmd.Memo?.Trim() is { Length: > 0 } m ? m : "Transfer between bank and cash accounts";
        var journal = await posting.PostAsync(new PostingRequest(cmd.LegalEntityId, cmd.Date, "transfer", null, memo, null,
            [new PostingLine(cmd.ToAccountId, cmd.Amount, 0, memo), new PostingLine(cmd.FromAccountId, 0, cmd.Amount, memo)]), ct);
        await lines.SaveChangesAsync(ct);
        return journal.Id;
    }

    public async Task<ReconciliationDto> Handle(GetReconciliation q, CancellationToken ct)
    {
        var account = await accounts.GetAsync(q.AccountId, ct);
        var all = await LedgerAsync(q.AccountId, q.LegalEntityId, q.AsOf, ct);
        var matchedIds = await MatchedIdsAsync(q.AccountId, ct);
        var statementOpen = await lines.Query(new Specification<BankStatementLine>(l => l.AccountId == q.AccountId && l.Date <= q.AsOf && l.Status == StatementLineStatus.Unmatched && (q.LegalEntityId == null || l.LegalEntityId == q.LegalEntityId)))
            .OrderBy(l => l.Date).ToListAsync(ct);
        var ledgerOpen = all.Where(x => !matchedIds.Contains(x.JournalLineId)).Select(ToItem).ToList();
        var matchedCount = await lines.CountAsync(l => l.AccountId == q.AccountId && l.Status == StatementLineStatus.Matched, ct);
        var stTotal = statementOpen.Sum(l => l.Amount); var ledTotal = ledgerOpen.Sum(l => l.Amount);
        return new ReconciliationDto(account.Id, account.Code, account.Name, q.AsOf, all.Sum(x => x.Debit - x.Credit), matchedCount, statementOpen.Select(ToDto).ToList(), ledgerOpen, stTotal, ledTotal, statementOpen.Count == 0 && ledgerOpen.Count == 0);
    }

    public async Task<CashPositionDto> Handle(GetCashPosition q, CancellationToken ct)
    {
        var cash = await accounts.Query(new Specification<Account>(a => a.IsActive && a.Type == AccountType.Asset && (a.IsBank || a.SystemRole == "cash" || a.SystemRole == "bank"))).OrderBy(a => a.Code)
            .Select(a => new { a.Id, a.Code, a.Name, a.IsBank }).ToListAsync(ct);
        var ids = cash.Select(c => c.Id).ToList();
        var movements = await journals.Query(new Specification<JournalEntry>(j => j.Status != JournalStatus.Draft && j.Date <= q.AsOf && (q.LegalEntityId == null || j.LegalEntityId == q.LegalEntityId)))
            .SelectMany(j => j.Lines.Where(l => ids.Contains(l.AccountId)), (j, l) => new { l.AccountId, l.Debit, l.Credit }).ToListAsync(ct);
        var rows = cash.Select(c => new CashPositionRow(c.Id, c.Code, c.Name, c.IsBank, movements.Where(m => m.AccountId == c.Id).Sum(m => m.Debit - m.Credit))).ToList();
        return new CashPositionDto(q.AsOf, rows, rows.Sum(r => r.Balance));
    }

    private async Task<List<LedgerRow>> LedgerAsync(Guid accountId, Guid? entityId, DateOnly asOf, CancellationToken ct)
        => await journals.Query(new Specification<JournalEntry>(j => j.Status != JournalStatus.Draft && j.Date <= asOf && (entityId == null || j.LegalEntityId == entityId)))
            .SelectMany(j => j.Lines.Where(l => l.AccountId == accountId), (j, l) => new LedgerRow(l.Id, j.Id, j.Number, j.Date, l.Description ?? j.Memo, l.Debit, l.Credit)).ToListAsync(ct);

    private async Task<HashSet<Guid>> MatchedIdsAsync(Guid accountId, CancellationToken ct)
        => (await lines.Query(new Specification<BankStatementLine>(l => l.AccountId == accountId && l.MatchedJournalLineId != null)).Select(l => l.MatchedJournalLineId!.Value).ToListAsync(ct)).ToHashSet();

    private async Task<List<LedgerItem>> UnmatchedLedgerAsync(Guid accountId, Guid entityId, DateOnly asOf, CancellationToken ct)
    {
        var all = await LedgerAsync(accountId, entityId, asOf, ct);
        var matched = await MatchedIdsAsync(accountId, ct);
        return all.Where(x => !matched.Contains(x.JournalLineId)).Select(ToItem).ToList();
    }

    private async Task RequireCashAccountAsync(Guid id, string field, CancellationToken ct)
    {
        var a = await accounts.FindAsync(id, ct);
        if (a is null || !BankAccountRules.IsCashOrBank(a)) throw new FieldValidationException(field, "Choose an active bank or cash account.");
    }

    private static LedgerItem ToItem(LedgerRow r) => new(r.JournalLineId, r.JournalId, r.Number, r.Date, r.Description, r.Debit - r.Credit);
    internal static StatementLineDto ToDto(BankStatementLine l) => new(l.Id, l.AccountId, l.Date, l.Description, l.Reference, l.Amount, l.Status, l.MatchedJournalLineId);
}
