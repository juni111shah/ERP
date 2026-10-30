using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Finance;

public sealed record JournalLineInput(Guid AccountId, decimal Debit, decimal Credit, string? Description = null, Guid? PartyId = null, Guid? BusinessUnitId = null);
public sealed record JournalInput(Guid LegalEntityId, DateOnly Date, string? Memo, string? Reference, List<JournalLineInput> Lines);

public sealed record JournalLineDto(Guid Id, int Seq, Guid AccountId, string AccountCode, string AccountName, decimal Debit, decimal Credit, string? Description, Guid? PartyId);
public sealed record JournalDto(
    Guid Id, string Number, Guid LegalEntityId, DateOnly Date, string? Memo, string? Reference, string SourceType, Guid? SourceId, string CurrencyCode,
    JournalStatus Status, decimal Amount, Guid? ReversalOfId, DateTime? PostedAt, IReadOnlyList<JournalLineDto> Lines);
public sealed record JournalSummaryDto(Guid Id, string Number, Guid LegalEntityId, DateOnly Date, string? Memo, string SourceType, JournalStatus Status, decimal Amount, string CurrencyCode);

public sealed record GetJournal(Guid Id) : IQuery<JournalDto>, IRequirePermission { public string RequiredPermission => CorePermissions.JournalsRead; }
public sealed record CreateJournal(JournalInput Input) : ICommand<JournalDto>, IRetryOnConcurrency, IRequirePermission { public string RequiredPermission => CorePermissions.JournalsWrite; }
public sealed record UpdateJournal(Guid Id, JournalInput Input) : ICommand<JournalDto>, IRequirePermission { public string RequiredPermission => CorePermissions.JournalsWrite; }
public sealed record DeleteJournal(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => CorePermissions.JournalsWrite; }
public sealed record PostJournal(Guid Id) : ICommand<JournalDto>, IRetryOnConcurrency, IRequirePermission { public string RequiredPermission => CorePermissions.JournalsPost; }
public sealed record ReverseJournal(Guid Id, DateOnly? Date, string? Memo) : ICommand<JournalDto>, IRequirePermission { public string RequiredPermission => CorePermissions.JournalsReverse; }
public sealed class ReverseJournalValidator : AbstractValidator<ReverseJournal> { public ReverseJournalValidator() => RuleFor(x => x.Memo).MaximumLength(400); }
/// <summary>Closes the books of a legal entity up to (and including) a date. Nothing can be posted on or before it.</summary>
public sealed record LockBooks(Guid LegalEntityId, DateOnly? Through) : ICommand, IRequirePermission { public string RequiredPermission => CorePermissions.EntitiesWrite; }

public sealed class JournalInputValidator : AbstractValidator<JournalInput>
{
    public JournalInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.Memo).MaximumLength(500);
        RuleFor(x => x.Reference).MaximumLength(100);
        RuleFor(x => x.Lines).NotNull().Must(l => l is { Count: >= 2 }).WithMessage("A journal needs at least two lines.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.AccountId).NotEmpty();
            l.RuleFor(x => x.Debit).GreaterThanOrEqualTo(0).MustBeMoney();
            l.RuleFor(x => x.Credit).GreaterThanOrEqualTo(0).MustBeMoney();
            l.RuleFor(x => x).Must(x => (x.Debit > 0) != (x.Credit > 0)).WithMessage("Each line needs either a debit or a credit.");
        });
    }
}

public sealed class CreateJournalValidator : AbstractValidator<CreateJournal> { public CreateJournalValidator() => RuleFor(x => x.Input).SetValidator(new JournalInputValidator()); }
public sealed class UpdateJournalValidator : AbstractValidator<UpdateJournal> { public UpdateJournalValidator() => RuleFor(x => x.Input).SetValidator(new JournalInputValidator()); }

internal sealed class JournalHandlers(
    IRepository<JournalEntry> journals, IRepository<JournalLine> lineRows, IReadRepository<Account> accounts, IRepository<Domain.Organizations.LegalEntity> entities, ILegalEntityGuard guard,
    IPostingService posting, INumberingService numbering, ICurrentUser user, IClock clock, IUnitOfWork uow) :
    IRequestHandler<GetJournal, JournalDto>,
    IRequestHandler<CreateJournal, JournalDto>,
    IRequestHandler<UpdateJournal, JournalDto>,
    IRequestHandler<DeleteJournal, Unit>,
    IRequestHandler<PostJournal, JournalDto>,
    IRequestHandler<ReverseJournal, JournalDto>,
    IRequestHandler<LockBooks, Unit>
{
    public async Task<JournalDto> Handle(GetJournal q, CancellationToken ct)
    {
        var j = await journals.Query(new Specification<JournalEntry>(x => x.Id == q.Id).Include(x => x.Lines)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        return await ToDtoAsync(j, ct);
    }

    public async Task<JournalDto> Handle(CreateJournal cmd, CancellationToken ct)
    {
        var entity = await guard.RequireAsync(cmd.Input.LegalEntityId, ct);
        var journal = new JournalEntry { LegalEntityId = entity.Id, CurrencyCode = entity.BaseCurrency, SourceType = "manual" };
        journal.Number = "DRAFT-" + journal.Id.ToString("N")[^8..]; // the real number is allocated when posted, so numbers have no gaps
        journal.Lines.AddRange(await ApplyAsync(journal, cmd.Input, ct));
        journals.Add(journal);
        await journals.SaveChangesAsync(ct);
        return await ToDtoAsync(journal, ct);
    }

    public async Task<JournalDto> Handle(UpdateJournal cmd, CancellationToken ct)
    {
        var journal = await LoadDraftAsync(cmd.Id, ct);
        if (cmd.Input.LegalEntityId != journal.LegalEntityId) throw new FieldValidationException("legalEntityId", "The legal entity of a journal cannot change.");
        // Replace the lines. New children are added through their own repository: a client-side GUID added through the
        // parent's navigation alone is treated as an existing row (concurrency error).
        foreach (var old in journal.Lines.ToList()) lineRows.Remove(old);
        var fresh = await ApplyAsync(journal, cmd.Input, ct);
        lineRows.AddRange(fresh);
        await journals.SaveChangesAsync(ct);
        journal.Lines.Clear();
        journal.Lines.AddRange(fresh);
        return await ToDtoAsync(journal, ct);
    }

    public async Task<Unit> Handle(DeleteJournal cmd, CancellationToken ct)
    {
        journals.Remove(await LoadDraftAsync(cmd.Id, ct));
        await journals.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public async Task<JournalDto> Handle(PostJournal cmd, CancellationToken ct)
    {
        var journal = await LoadDraftAsync(cmd.Id, ct);
        var entity = await guard.RequireAsync(journal.LegalEntityId, ct);
        await EnsurePostableAsync(journal.Lines.Select(l => l.AccountId).Distinct().ToList(), ct); // an account may have been deactivated since the draft was saved
        journal.Recalculate();
        journal.EnsureCanPost(entity.BooksLockedTo);
        journal.Number = await numbering.NextAsync("journal", entity.Id, journal.Date, ct);
        journal.Post(user.UserId ?? Guid.Empty, clock.UtcNow, entity.BooksLockedTo);
        await journals.SaveChangesAsync(ct);
        return await ToDtoAsync(journal, ct);
    }

    private static readonly HashSet<string> DocumentSourceTypes = new(StringComparer.OrdinalIgnoreCase) { "invoice", "creditnote", "bill", "receipt", "payment", "cheque" };
    private const string ManualSource = "manual";

    public async Task<JournalDto> Handle(ReverseJournal cmd, CancellationToken ct)
    {
        var original = await journals.Tracked(new Specification<JournalEntry>(x => x.Id == cmd.Id).Include(x => x.Lines)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        // Journals owned by a document with its own void / cancel flow must be undone there, or the document and ledger drift apart.
        // Only journals entered by hand can be reversed here. Anything the system posted (documents, modules, automatic postings)
        // must be undone through its own flow, or the source record and the ledger drift apart.
        if (original.ReversalOfId is null && !string.Equals(original.SourceType, ManualSource, StringComparison.OrdinalIgnoreCase))
        {
            var src = original.SourceType;
            throw new ConflictException(DocumentSourceTypes.Contains(src ?? "")
                ? $"This entry was created by a source document ({src}). Void the document instead of reversing its journal."
                : $"This entry was posted automatically by the system ({src}). Only manual journals can be reversed; undo it from where it was created.");
        }
        var reversal = cmd.Date is { } date
            ? await posting.ReverseAsync(original, date, cmd.Memo, ct)
            : await posting.ReverseTodayAsync(original, cmd.Memo, ct);
        await journals.SaveChangesAsync(ct);
        return await ToDtoAsync(reversal, ct);
    }

    public async Task<Unit> Handle(LockBooks cmd, CancellationToken ct)
    {
        await guard.RequireAsync(cmd.LegalEntityId, ct);
        if (cmd.Through is { } d && d > DateOnly.FromDateTime(clock.UtcNow)) throw new FieldValidationException("through", "Books cannot be locked to a future date.");
        var entity = await entities.GetForUpdateAsync(cmd.LegalEntityId, ct);
        entity.BooksLockedTo = cmd.Through;
        await entities.SaveChangesAsync(ct);
        return Unit.Value;
    }

    private async Task<JournalEntry> LoadDraftAsync(Guid id, CancellationToken ct)
    {
        var j = await journals.Tracked(new Specification<JournalEntry>(x => x.Id == id).Include(x => x.Lines)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        if (j.Status != JournalStatus.Draft) throw new ConflictException("Only a draft journal can be changed. Reverse a posted one instead.");
        if (j.SourceType != "manual") throw new ConflictException("This entry was created by a document and cannot be edited here.");
        return j;
    }

    /// <summary>Manual journals bypass <see cref="IPostingService.PostAsync"/>, so they check the accounts themselves: found, active, not a header.</summary>
    private async Task EnsurePostableAsync(List<Guid> ids, CancellationToken ct)
    {
        var found = await accounts.Query(new Specification<Account>(a => ids.Contains(a.Id))).Select(a => new { a.Id, a.Code, a.IsPostable, a.IsActive }).ToListAsync(ct);
        if (found.Count != ids.Count) throw new FieldValidationException("lines", "One of the accounts was not found.");
        var bad = found.FirstOrDefault(a => !a.IsPostable || !a.IsActive);
        if (bad is not null) throw new FieldValidationException("lines", $"Account {bad.Code} cannot be posted to (inactive or a header account).");
    }

    private async Task<List<JournalLine>> ApplyAsync(JournalEntry journal, JournalInput i, CancellationToken ct)
    {
        var ids = i.Lines.Select(l => l.AccountId).Distinct().ToList();
        await EnsurePostableAsync(ids, ct);
        journal.Date = i.Date; journal.Memo = i.Memo?.Trim(); journal.Reference = i.Reference?.Trim();
        var seq = 1;
        var lines = i.Lines.Select(l => new JournalLine { JournalEntryId = journal.Id, Seq = seq++, AccountId = l.AccountId, Debit = l.Debit, Credit = l.Credit, Description = l.Description, PartyId = l.PartyId, BusinessUnitId = l.BusinessUnitId }).ToList();
        journal.Amount = lines.Sum(l => l.Debit);
        return lines;
    }

    private async Task<JournalDto> ToDtoAsync(JournalEntry j, CancellationToken ct)
    {
        var ids = j.Lines.Select(l => l.AccountId).Distinct().ToList();
        var names = (await accounts.Query(new Specification<Account>(a => ids.Contains(a.Id))).Select(a => new { a.Id, a.Code, a.Name }).ToListAsync(ct)).ToDictionary(a => a.Id);
        var lines = j.Lines.OrderBy(l => l.Seq).Select(l => new JournalLineDto(l.Id, l.Seq, l.AccountId, names[l.AccountId].Code, names[l.AccountId].Name, l.Debit, l.Credit, l.Description, l.PartyId)).ToList();
        return new JournalDto(j.Id, j.Number, j.LegalEntityId, j.Date, j.Memo, j.Reference, j.SourceType, j.SourceId, j.CurrencyCode, j.Status, j.Amount, j.ReversalOfId, j.PostedAt, lines);
    }
}
