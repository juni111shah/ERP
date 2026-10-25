using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Finance;

public sealed record PostingLine(Guid AccountId, decimal Debit, decimal Credit, string? Description = null, Guid? PartyId = null, Guid? BusinessUnitId = null);

/// <summary>What a business document (invoice, receipt, payroll, ...) asks the ledger to record.</summary>
public sealed record PostingRequest(
    Guid LegalEntityId, DateOnly Date, string SourceType, Guid? SourceId, string? Memo, string? Reference, IReadOnlyList<PostingLine> Lines,
    string? CurrencyCode = null, decimal ExchangeRate = 1m);

/// <summary>
/// The single way money enters the ledger. Documents never write journal lines themselves: they describe the entry and this service
/// validates it (balanced, postable accounts, open period), numbers it and adds it to the caller's unit of work (the caller saves).
/// </summary>
public interface IPostingService
{
    Task<JournalEntry> PostAsync(PostingRequest request, CancellationToken ct = default);
    /// <summary>Posts the opposite entry and marks the original reversed.</summary>
    Task<JournalEntry> ReverseAsync(JournalEntry original, DateOnly date, string? memo, CancellationToken ct = default);
    /// <summary>Reverses dated today (never before the original), so voids still work after the original period is locked.</summary>
    Task<JournalEntry> ReverseTodayAsync(JournalEntry original, string? memo, CancellationToken ct = default);
    /// <summary>The account that carries a system role (for example "ar.control") in this workspace.</summary>
    Task<Guid> AccountForRoleAsync(string systemRole, CancellationToken ct = default);
}

public static class SystemRoles
{
    public const string ArControl = "ar.control";
    public const string ApControl = "ap.control";
    public const string TaxOutput = "tax.output";
    public const string TaxInput = "tax.input";
    public const string Fx = "fx.gainloss";
    public const string InventoryClearing = "inventory.clearing";
    public const string Wip = "wip";
    public const string PurchaseVariance = "purchase.variance";
}

public sealed class PostingService(
    IRepository<JournalEntry> journals, IReadRepository<Account> accounts, ILegalEntityGuard guard, INumberingService numbering, ICurrentUser user, IClock clock) : IPostingService
{
    public async Task<JournalEntry> PostAsync(PostingRequest r, CancellationToken ct = default)
    {
        var entity = await guard.RequireAsync(r.LegalEntityId, ct);
        await EnsureAccountsAsync(r.Lines.Select(l => l.AccountId), ct);
        var journal = new JournalEntry
        {
            LegalEntityId = entity.Id, Date = r.Date, Memo = r.Memo, Reference = r.Reference, SourceType = r.SourceType, SourceId = r.SourceId,
            CurrencyCode = r.CurrencyCode ?? entity.BaseCurrency, ExchangeRate = r.ExchangeRate, Number = "pending",
        };
        var seq = 1;
        foreach (var l in r.Lines)
            journal.Lines.Add(new JournalLine { JournalEntryId = journal.Id, Seq = seq++, AccountId = l.AccountId, Debit = l.Debit, Credit = l.Credit, Description = l.Description, PartyId = l.PartyId, BusinessUnitId = l.BusinessUnitId });
        journal.Recalculate();
        journal.EnsureCanPost(entity.BooksLockedTo); // validate before consuming a number
        journal.Number = await numbering.NextAsync("journal", entity.Id, r.Date, ct);
        journal.Post(user.UserId ?? Guid.Empty, clock.UtcNow, entity.BooksLockedTo);
        journals.Add(journal);
        return journal;
    }

    public async Task<JournalEntry> ReverseAsync(JournalEntry original, DateOnly date, string? memo, CancellationToken ct = default)
    {
        if (original.Status != JournalStatus.Posted) throw new ConflictException("Only a posted entry can be reversed.");
        if (original.ReversalOfId is not null) throw new ConflictException("A reversal entry cannot be reversed again; post a new entry instead.");
        var lines = original.Lines.Select(l => new PostingLine(l.AccountId, l.Credit, l.Debit, l.Description, l.PartyId, l.BusinessUnitId)).ToList();
        var reversal = await PostAsync(new PostingRequest(original.LegalEntityId, date, "reversal", original.Id, memo ?? $"Reversal of {original.Number}", original.Number, lines, original.CurrencyCode, original.ExchangeRate), ct);
        reversal.ReversalOfId = original.Id;
        original.Status = JournalStatus.Reversed;
        return reversal;
    }

    public Task<JournalEntry> ReverseTodayAsync(JournalEntry original, string? memo, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow);
        return ReverseAsync(original, today > original.Date ? today : original.Date, memo, ct);
    }

    public async Task<Guid> AccountForRoleAsync(string systemRole, CancellationToken ct = default)
    {
        var id = await accounts.Query(new Specification<Account>(a => a.SystemRole == systemRole && a.IsActive)).Select(a => (Guid?)a.Id).FirstOrDefaultAsync(ct);
        return id ?? throw new ConflictException($"No account is set up for '{systemRole}'. Add one in the chart of accounts.");
    }

    private async Task EnsureAccountsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var wanted = ids.Distinct().ToList();
        var found = await accounts.Query(new Specification<Account>(a => wanted.Contains(a.Id))).Select(a => new { a.Id, a.IsPostable, a.IsActive, a.Code }).ToListAsync(ct);
        if (found.Count != wanted.Count) throw new FieldValidationException("lines", "One of the accounts was not found.");
        var bad = found.FirstOrDefault(a => !a.IsPostable || !a.IsActive);
        if (bad is not null) throw new FieldValidationException("lines", $"Account {bad.Code} cannot be posted to (inactive or a header account).");
    }
}
