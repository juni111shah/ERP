using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Application.Parties;
using ERP.Application.Security;
using ERP.Domain.Finance;
using ERP.Domain.Organizations;
using ERP.Domain.Parties;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Finance;

/// <summary>One balance at go-live. On the receivables and payables control accounts each line is one open document of a party (with its own due date and reference).</summary>
public sealed record OpeningBalanceLineInput(Guid AccountId, decimal Debit, decimal Credit, Guid? PartyId = null, string? Description = null, Guid? BusinessUnitId = null, DateOnly? DueDate = null, string? Reference = null);
public sealed record OpeningBalanceInput(Guid LegalEntityId, DateOnly Date, List<OpeningBalanceLineInput> Lines, string? Memo = null);

public sealed record OpeningBalanceDto(
    Guid JournalId, string Number, Guid LegalEntityId, DateOnly Date, JournalStatus Status, decimal Amount, decimal OpeningEquityAmount,
    IReadOnlyList<Guid> InvoiceIds, IReadOnlyList<Guid> BillIds, Guid? ReversalJournalId = null);

/// <summary>
/// Posts the opening entry of a legal entity: the balances given, with the difference to the opening balance equity account. Receivable and payable lines also
/// raise an open invoice or bill, because that is what the ageing, the statements and later receipts and payments work from, so the subledger ties to the control account.
/// Refused while an earlier opening entry of the entity stands; reverse that one first.
/// </summary>
public sealed record PostOpeningBalance(OpeningBalanceInput Input) : ICommand<OpeningBalanceDto>, IRetryOnConcurrency, IRequirePermission
{
    public string RequiredPermission => CorePermissions.JournalsPost;
}

/// <summary>Reverses an opening entry (dated like the entry unless a date is given) and voids the documents it raised. Refused once any of them has been settled.</summary>
public sealed record ReverseOpeningBalance(Guid JournalId, string? Reason, DateOnly? Date = null) : ICommand<OpeningBalanceDto>, IRetryOnConcurrency, IRequirePermission
{
    public string RequiredPermission => CorePermissions.JournalsReverse;
}

public sealed class PostOpeningBalanceValidator : AbstractValidator<PostOpeningBalance>
{
    public PostOpeningBalanceValidator()
    {
        RuleFor(x => x.Input.LegalEntityId).NotEmpty();
        RuleFor(x => x.Input.Date).MustBePlausibleDate();
        RuleFor(x => x.Input.Memo).MaximumLength(400);
        RuleFor(x => x.Input.Lines).NotNull().Must(l => l is { Count: > 0 }).WithMessage("Enter at least one opening balance.");
        RuleForEach(x => x.Input.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.AccountId).NotEmpty();
            l.RuleFor(x => x.Debit).GreaterThanOrEqualTo(0).MustBeMoney();
            l.RuleFor(x => x.Credit).GreaterThanOrEqualTo(0).MustBeMoney();
            l.RuleFor(x => x).Must(x => (x.Debit > 0) != (x.Credit > 0)).WithMessage("Each line needs either a debit or a credit.");
            l.RuleFor(x => x.Description).MaximumLength(400);
            l.RuleFor(x => x.Reference).MaximumLength(100);
            l.RuleFor(x => x.DueDate).MustBePlausibleDate();
        });
    }
}

public sealed class ReverseOpeningBalanceValidator : AbstractValidator<ReverseOpeningBalance>
{
    public ReverseOpeningBalanceValidator()
    {
        RuleFor(x => x.JournalId).NotEmpty();
        RuleFor(x => x.Reason).ValidVoidReason();
        RuleFor(x => x.Date).MustBePlausibleDate();
    }
}

internal sealed class OpeningBalanceHandlers(
    IRepository<JournalEntry> journals, IRepository<Invoice> invoices, IRepository<Bill> bills, IReadRepository<Account> accounts, IReadRepository<Party> parties, IReadRepository<BusinessUnit> businessUnits,
    ILegalEntityGuard guard, IPostingService posting, IUnitOfWork uow, ICurrentUser user, IClock clock) :
    IRequestHandler<PostOpeningBalance, OpeningBalanceDto>,
    IRequestHandler<ReverseOpeningBalance, OpeningBalanceDto>
{
    public Task<OpeningBalanceDto> Handle(PostOpeningBalance cmd, CancellationToken ct) => uow.InTransactionAsync(token => PostCoreAsync(cmd.Input, token), ct);

    public Task<OpeningBalanceDto> Handle(ReverseOpeningBalance cmd, CancellationToken ct) => uow.InTransactionAsync(token => ReverseCoreAsync(cmd, token), ct);

    private async Task<OpeningBalanceDto> PostCoreAsync(OpeningBalanceInput i, CancellationToken ct)
    {
        var entity = await guard.RequireAsync(i.LegalEntityId, ct);
        if (await journals.AnyAsync(j => j.LegalEntityId == entity.Id && j.SourceType == LedgerSources.Opening && j.ReversalOfId == null && j.Status == JournalStatus.Posted, ct))
            throw new ConflictException("This legal entity already has an opening entry. Reverse it before posting another.");

        var ids = i.Lines.Select(l => l.AccountId).Distinct().ToList();
        var chart = await accounts.Query(new Specification<Account>(a => ids.Contains(a.Id))).ToDictionaryAsync(a => a.Id, ct);
        if (chart.Count != ids.Count) throw new FieldValidationException("lines", "One of the accounts was not found.");
        var partyIds = i.Lines.Where(l => l.PartyId is not null).Select(l => l.PartyId!.Value).Distinct().ToList();
        var partyRows = await parties.Query(new Specification<Party>(p => partyIds.Contains(p.Id))).ToDictionaryAsync(p => p.Id, ct);
        if (partyRows.Count != partyIds.Count) throw new FieldValidationException("lines", "One of the parties was not found.");
        var unitIds = i.Lines.Where(l => l.BusinessUnitId is not null).Select(l => l.BusinessUnitId!.Value).Distinct().ToList();
        if (await businessUnits.CountAsync(u => unitIds.Contains(u.Id) && u.LegalEntityId == entity.Id, ct) != unitIds.Count)
            throw new FieldValidationException("lines", "A business unit was not found in this legal entity.");

        var memo = string.IsNullOrWhiteSpace(i.Memo) ? "Opening balances" : i.Memo.Trim();
        var lines = new List<PostingLine>();
        foreach (var l in i.Lines)
        {
            var account = chart[l.AccountId];
            if (account.Type is AccountType.Income or AccountType.Expense)
                throw new FieldValidationException("lines", $"Account {account.Code} is an income or expense account. Opening balances are for balance sheet accounts.");
            if (account.SystemRole == SystemRoles.OpeningEquity)
                throw new FieldValidationException("lines", $"Account {account.Code} takes the difference automatically. Leave it out of the list.");
            if (account.SystemRole == SystemRoles.ArControl) CustomerRules.RequireCustomer(RequireParty(l, partyRows, account));
            if (account.SystemRole == SystemRoles.ApControl)
            {
                VendorRules.RequireVendor(RequireParty(l, partyRows, account));
                if (l.Debit > 0) throw new FieldValidationException("lines", "A vendor's opening balance must be a credit. Enter money paid in advance on an advances account.");
            }
            lines.Add(new PostingLine(l.AccountId, l.Debit, l.Credit, l.Description?.Trim() ?? memo, l.PartyId, l.BusinessUnitId));
        }

        var difference = i.Lines.Sum(l => l.Debit - l.Credit);
        if (difference != 0)
        {
            var equity = await posting.AccountForRoleAsync(SystemRoles.OpeningEquity, ct);
            lines.Add(difference > 0 ? new PostingLine(equity, 0, difference, memo) : new PostingLine(equity, -difference, 0, memo));
        }
        if (lines.Count < 2) throw new FieldValidationException("lines", "A single line that nets to nothing cannot be posted.");

        var journal = await posting.PostAsync(new PostingRequest(entity.Id, i.Date, LedgerSources.Opening, null, memo, "OPENING", lines, entity.BaseCurrency), ct);
        var invoiceIds = new List<Guid>();
        var billIds = new List<Guid>();
        var seq = 1;
        foreach (var l in i.Lines)
        {
            var role = chart[l.AccountId].SystemRole;
            if (role == SystemRoles.ArControl)
            {
                var party = partyRows[l.PartyId!.Value];
                var doc = new Invoice
                {
                    LegalEntityId = entity.Id, Number = $"OB-{journal.Number}-{seq++}", IsCreditNote = l.Credit > 0, PartyId = party.Id, PartyName = party.Name, InvoiceDate = i.Date, DueDate = l.DueDate ?? i.Date,
                    CurrencyCode = entity.BaseCurrency, Net = l.Debit + l.Credit, Total = l.Debit + l.Credit, Status = DocumentStatus.Issued, SourceType = LedgerSources.Opening, SourceId = journal.Id,
                    BusinessUnitId = l.BusinessUnitId, JournalEntryId = journal.Id, Notes = OpeningNote(l),
                };
                invoices.Add(doc);
                invoiceIds.Add(doc.Id);
            }
            else if (role == SystemRoles.ApControl)
            {
                var party = partyRows[l.PartyId!.Value];
                var doc = new Bill
                {
                    LegalEntityId = entity.Id, Number = $"OB-{journal.Number}-{seq++}", PartyId = party.Id, PartyName = party.Name, BillDate = i.Date, DueDate = l.DueDate ?? i.Date, CurrencyCode = entity.BaseCurrency,
                    Net = l.Credit, Total = l.Credit, Status = DocumentStatus.Issued, SourceType = LedgerSources.Opening, SourceId = journal.Id, BusinessUnitId = l.BusinessUnitId, JournalEntryId = journal.Id, Notes = OpeningNote(l),
                };
                bills.Add(doc);
                billIds.Add(doc.Id);
            }
        }
        await uow.SaveChangesAsync(ct);
        return new OpeningBalanceDto(journal.Id, journal.Number, entity.Id, journal.Date, journal.Status, journal.Amount, difference, invoiceIds, billIds);
    }

    private async Task<OpeningBalanceDto> ReverseCoreAsync(ReverseOpeningBalance cmd, CancellationToken ct)
    {
        var journal = await journals.Tracked(new Specification<JournalEntry>(j => j.Id == cmd.JournalId && j.SourceType == LedgerSources.Opening && j.ReversalOfId == null).Include(j => j.Lines)).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Opening entry not found.");
        if (journal.Status != JournalStatus.Posted) throw new ConflictException("This opening entry has already been reversed.");
        var openInvoices = await invoices.Tracked(new Specification<Invoice>(d => d.SourceType == LedgerSources.Opening && d.SourceId == journal.Id)).ToListAsync(ct);
        var openBills = await bills.Tracked(new Specification<Bill>(d => d.SourceType == LedgerSources.Opening && d.SourceId == journal.Id)).ToListAsync(ct);
        if (openInvoices.Any(d => d.Paid != 0) || openBills.Any(d => d.Paid != 0))
            throw new ConflictException("Some opening documents have been settled. Void those receipts and payments first.");

        var reason = cmd.Reason!.Trim();
        var reversal = await posting.ReverseAsync(journal, cmd.Date ?? journal.Date, VoidMemo.For(journal.Number, reason), ct);
        reversal.SourceType = LedgerSources.Opening; // keeps its cash out of the cash flow statement, as the entry it undoes
        foreach (var d in openInvoices) { d.Status = DocumentStatus.Voided; VoidMemo.Stamp(d, reason, clock, user); }
        foreach (var d in openBills) { d.Status = DocumentStatus.Voided; VoidMemo.Stamp(d, reason, clock, user); }
        await uow.SaveChangesAsync(ct);
        var equity = await posting.AccountForRoleAsync(SystemRoles.OpeningEquity, ct);
        return new OpeningBalanceDto(journal.Id, journal.Number, journal.LegalEntityId, journal.Date, journal.Status, journal.Amount, journal.Lines.Where(l => l.AccountId == equity).Sum(l => l.Credit - l.Debit),
            openInvoices.Select(d => d.Id).ToList(), openBills.Select(d => d.Id).ToList(), reversal.Id);
    }

    private static Party RequireParty(OpeningBalanceLineInput line, Dictionary<Guid, Party> partyRows, Account account)
        => line.PartyId is { } id ? partyRows[id] : throw new FieldValidationException("lines", $"Choose the party for the balance on {account.Code}, so the subledger ties to the control account.");

    private static string? OpeningNote(OpeningBalanceLineInput l)
    {
        var parts = new[] { l.Reference?.Trim(), l.Description?.Trim() }.Where(p => !string.IsNullOrEmpty(p)).ToList();
        return parts.Count == 0 ? "Opening balance" : "Opening balance: " + string.Join(" - ", parts);
    }
}
