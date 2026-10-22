using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Domain.Catalog;
using ERP.Domain.Finance;
using ERP.Domain.Parties;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Finance;

public sealed record InvoiceLineDraft(string Description, decimal Quantity, decimal Price, Guid? TaxCodeId = null, Guid? AccountId = null, Guid? ItemId = null);

/// <summary>What a caller (a screen, or a module's billing run) wants invoiced.</summary>
public sealed record InvoiceDraft(
    Guid LegalEntityId, Guid PartyId, DateOnly InvoiceDate, DateOnly? DueDate, IReadOnlyList<InvoiceLineDraft> Lines, bool IsCreditNote = false,
    string? SourceType = null, Guid? SourceId = null, Guid? BusinessUnitId = null, string? Notes = null);

/// <summary>Raised after an invoice is voided so the module that created it (via SourceType / SourceId) can release what it linked.</summary>
public sealed record InvoiceVoided(Guid InvoiceId, string? SourceType, Guid? SourceId) : INotification;

/// <summary>Builds, issues and voids sales invoices and credit notes. Shared by the invoice screens and by module billing runs.</summary>
public interface IInvoicingService
{
    /// <summary>Computes line and document totals and adds a draft invoice to the unit of work (the caller saves).</summary>
    Task<Invoice> BuildAsync(InvoiceDraft draft, CancellationToken ct = default);
    /// <summary>Recomputes a draft from new content (lines are replaced by the caller).</summary>
    Task<List<InvoiceLine>> ApplyAsync(Invoice invoice, InvoiceDraft draft, CancellationToken ct = default);
    /// <summary>Numbers the document and posts it to the ledger: Dr receivables, Cr income and output tax (reversed for a credit note).</summary>
    Task IssueAsync(Invoice invoice, CancellationToken ct = default);
    Task VoidAsync(Invoice invoice, CancellationToken ct = default);
}

public sealed class InvoicingService(
    IRepository<Invoice> invoices, IRepository<JournalEntry> journals, IReadRepository<Party> parties, IReadRepository<TaxCode> taxCodes, IReadRepository<Item> items,
    ILegalEntityGuard guard, IPostingService posting, INumberingService numbering, IPublisher publisher) : IInvoicingService
{
    public async Task<Invoice> BuildAsync(InvoiceDraft d, CancellationToken ct = default)
    {
        var invoice = new Invoice { LegalEntityId = d.LegalEntityId, Status = DocumentStatus.Draft };
        invoice.Number = "DRAFT-" + invoice.Id.ToString("N")[^8..];
        var lines = await ApplyAsync(invoice, d, ct);
        invoice.Lines.AddRange(lines);
        invoices.Add(invoice);
        return invoice;
    }

    public async Task<List<InvoiceLine>> ApplyAsync(Invoice invoice, InvoiceDraft d, CancellationToken ct = default)
    {
        var entity = await guard.RequireAsync(d.LegalEntityId, ct);
        var party = await parties.FindAsync(d.PartyId, ct) ?? throw new FieldValidationException("partyId", "Customer not found.");
        if (d.Lines.Count == 0) throw new FieldValidationException("lines", "An invoice needs at least one line.");

        var taxIds = d.Lines.Where(l => l.TaxCodeId is not null).Select(l => l.TaxCodeId!.Value).Distinct().ToList();
        var rates = await taxCodes.Query(new Specification<TaxCode>(t => taxIds.Contains(t.Id))).Select(t => new { t.Id, t.Rate }).ToDictionaryAsync(t => t.Id, t => t.Rate, ct);
        if (rates.Count != taxIds.Count) throw new FieldValidationException("lines", "A tax code was not found.");
        var itemIds = d.Lines.Where(l => l.ItemId is not null).Select(l => l.ItemId!.Value).Distinct().ToList();
        var itemTypes = await items.Query(new Specification<Item>(i => itemIds.Contains(i.Id))).Select(i => new { i.Id, i.Type }).ToDictionaryAsync(i => i.Id, i => i.Type, ct);

        var seq = 1;
        var result = new List<InvoiceLine>();
        foreach (var l in d.Lines)
        {
            if (l.Quantity <= 0 || l.Price < 0) throw new FieldValidationException("lines", "Quantity must be positive and the price cannot be negative.");
            var net = Money.Round(l.Quantity * l.Price);
            var tax = l.TaxCodeId is { } t ? Money.Round(net * rates[t] / 100m) : 0m;
            var account = l.AccountId ?? await posting.AccountForRoleAsync(l.ItemId is { } iid && itemTypes.TryGetValue(iid, out var type) && type == ItemType.Product ? "income.sales" : "income.services", ct);
            result.Add(new InvoiceLine { InvoiceId = invoice.Id, Seq = seq++, ItemId = l.ItemId, Description = l.Description, Quantity = l.Quantity, Price = l.Price, TaxCodeId = l.TaxCodeId, TaxAmount = tax, Net = net, AccountId = account });
        }
        invoice.PartyId = party.Id; invoice.PartyName = party.Name; invoice.IsCreditNote = d.IsCreditNote;
        invoice.InvoiceDate = d.InvoiceDate;
        invoice.DueDate = d.DueDate ?? d.InvoiceDate.AddDays(party.PaymentTermsDays ?? 0);
        if (invoice.DueDate < invoice.InvoiceDate) throw new FieldValidationException("dueDate", "The due date cannot be before the invoice date.");
        invoice.CurrencyCode = entity.BaseCurrency; invoice.ExchangeRate = 1m;
        invoice.SourceType = d.SourceType; invoice.SourceId = d.SourceId; invoice.BusinessUnitId = d.BusinessUnitId; invoice.Notes = d.Notes;
        invoice.Net = result.Sum(l => l.Net); invoice.Tax = result.Sum(l => l.TaxAmount); invoice.Total = invoice.Net + invoice.Tax;
        return result;
    }

    public async Task IssueAsync(Invoice invoice, CancellationToken ct = default)
    {
        if (invoice.Status != DocumentStatus.Draft) throw new ConflictException("Only a draft can be issued.");
        if (invoice.Total <= 0) throw new ConflictException("An invoice with a zero total cannot be issued.");
        var sign = invoice.IsCreditNote ? -1 : 1; // a credit note posts the opposite of an invoice
        var ar = await posting.AccountForRoleAsync(SystemRoles.ArControl, ct);
        var memo = (invoice.IsCreditNote ? "Credit note " : "Invoice ") + invoice.PartyName;
        invoice.Number = await numbering.NextAsync(invoice.IsCreditNote ? "creditnote" : "invoice", invoice.LegalEntityId, invoice.InvoiceDate, ct);

        var lines = new List<PostingLine> { Line(ar, sign * invoice.Total, memo, invoice.PartyId) };
        foreach (var g in invoice.Lines.GroupBy(l => l.AccountId!.Value))
            lines.Add(Line(g.Key, -sign * g.Sum(l => l.Net), memo, null));
        if (invoice.Tax > 0) lines.Add(Line(await posting.AccountForRoleAsync(SystemRoles.TaxOutput, ct), -sign * invoice.Tax, memo, null));

        lines.RemoveAll(l => l.Debit == 0 && l.Credit == 0);
        var journal = await posting.PostAsync(new PostingRequest(invoice.LegalEntityId, invoice.InvoiceDate, invoice.IsCreditNote ? "creditnote" : "invoice", invoice.Id, memo, invoice.Number, lines, invoice.CurrencyCode), ct);
        invoice.JournalEntryId = journal.Id;
        invoice.Status = DocumentStatus.Issued;
    }

    public async Task VoidAsync(Invoice invoice, CancellationToken ct = default)
    {
        if (invoice.Status is not DocumentStatus.Issued || invoice.Paid != 0) throw new ConflictException("Only an issued document with nothing paid or applied can be voided. Issue a credit note instead.");
        var journal = await journals.Tracked(new Specification<JournalEntry>(j => j.Id == invoice.JournalEntryId).Include(j => j.Lines)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        await posting.ReverseAsync(journal, journal.Date, $"Void {invoice.Number}", ct);
        invoice.Status = DocumentStatus.Voided;
        await publisher.Publish(new InvoiceVoided(invoice.Id, invoice.SourceType, invoice.SourceId), ct);
    }

    /// <summary>Positive amount = debit, negative = credit.</summary>
    private static PostingLine Line(Guid account, decimal signed, string memo, Guid? party)
        => signed >= 0 ? new PostingLine(account, signed, 0, memo, party) : new PostingLine(account, 0, -signed, memo, party);
}

public static class Money
{
    public static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
