using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Application.Parties;
using ERP.Domain.Finance;
using ERP.Domain.Organizations;
using ERP.Domain.Parties;

namespace ERP.Application.Finance;

internal sealed class PostOpeningBalanceHandler(
    IJournalEntryRepository journals, IAccountRepository accounts, IRepository<Invoice> invoices, IRepository<Bill> bills, OpeningBalanceDocuments documents,
    IReadRepository<BusinessUnit> businessUnits, ILegalEntityGuard guard, IPostingService posting, IUnitOfWork uow)
    : ICommandHandler<PostOpeningBalanceCommand, OpeningBalanceDto>
{
    public Task<OpeningBalanceDto> Handle(PostOpeningBalanceCommand cmd, CancellationToken ct) => uow.InTransactionAsync(token => PostAsync(cmd.Input, token), ct);

    private async Task<OpeningBalanceDto> PostAsync(OpeningBalanceInput i, CancellationToken ct)
    {
        // 1. One standing opening entry per legal entity.
        var entity = await guard.RequireAsync(i.LegalEntityId, ct);
        if (await journals.AnyAsync(j => j.LegalEntityId == entity.Id && j.SourceType == LedgerSources.Opening && j.ReversalOfId == null && j.Status == JournalStatus.Posted, ct))
            throw new ConflictException("This legal entity already has an opening entry. Reverse it before posting another.");

        // 2. Every account, party and business unit named exists.
        var ids = i.Lines.Select(l => l.AccountId).Distinct().ToList();
        var chart = await accounts.MapByIdsAsync(ids, ct);
        if (chart.Count != ids.Count) throw new FieldValidationException("lines", "One of the accounts was not found.");
        var partyIds = i.Lines.Where(l => l.PartyId is not null).Select(l => l.PartyId!.Value).Distinct().ToList();
        var partyRows = await documents.MapPartiesAsync(partyIds, ct);
        if (partyRows.Count != partyIds.Count) throw new FieldValidationException("lines", "One of the parties was not found.");
        var unitIds = i.Lines.Where(l => l.BusinessUnitId is not null).Select(l => l.BusinessUnitId!.Value).Distinct().ToList();
        if (await businessUnits.CountAsync(u => unitIds.Contains(u.Id) && u.LegalEntityId == entity.Id, ct) != unitIds.Count)
            throw new FieldValidationException("lines", "A business unit was not found in this legal entity.");

        // 3. The posting lines: balance sheet accounts only; receivables and payables name their customer or vendor.
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

        // 4. The difference goes to opening balance equity.
        var difference = i.Lines.Sum(l => l.Debit - l.Credit);
        if (difference != 0)
        {
            var equity = await posting.AccountForRoleAsync(SystemRoles.OpeningEquity, ct);
            lines.Add(difference > 0 ? new PostingLine(equity, 0, difference, memo) : new PostingLine(equity, -difference, 0, memo));
        }
        if (lines.Count < 2) throw new FieldValidationException("lines", "A single line that nets to nothing cannot be posted.");

        // 5. Post, then raise one open invoice or bill per receivable or payable line, and save.
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

    private static Party RequireParty(OpeningBalanceLineInput line, Dictionary<Guid, Party> partyRows, Account account)
        => line.PartyId is { } id ? partyRows[id] : throw new FieldValidationException("lines", $"Choose the party for the balance on {account.Code}, so the subledger ties to the control account.");

    private static string? OpeningNote(OpeningBalanceLineInput l)
    {
        var parts = new[] { l.Reference?.Trim(), l.Description?.Trim() }.Where(p => !string.IsNullOrEmpty(p)).ToList();
        return parts.Count == 0 ? "Opening balance" : "Opening balance: " + string.Join(" - ", parts);
    }
}
