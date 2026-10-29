using ERP.Domain.Platform;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Finance;

public sealed record InvoiceLineInput(string Description, decimal Quantity, decimal Price, Guid? TaxCodeId = null, Guid? AccountId = null, Guid? ItemId = null);
public sealed record InvoiceInput(Guid LegalEntityId, Guid PartyId, DateOnly InvoiceDate, DateOnly? DueDate, List<InvoiceLineInput> Lines, bool IsCreditNote = false, string? Notes = null);

public sealed record InvoiceLineDto(Guid Id, int Seq, Guid? ItemId, string Description, decimal Quantity, decimal Price, Guid? TaxCodeId, decimal TaxAmount, decimal Net, Guid? AccountId);
public sealed record InvoiceDto(
    Guid Id, string Number, bool IsCreditNote, Guid LegalEntityId, Guid PartyId, string? PartyName, DateOnly InvoiceDate, DateOnly DueDate, string CurrencyCode, decimal Net, decimal Tax,
    decimal Total, decimal Paid, DocumentStatus Status, string? SourceType, Guid? SourceId, Guid? JournalEntryId, string? Notes, IReadOnlyList<InvoiceLineDto> Lines,
    Guid? CreditNoteForId = null, string? VoidReason = null, DateTime? VoidedAt = null, IReadOnlyList<string>? Warnings = null)
{
    public decimal Outstanding => Total - Paid;
    public static InvoiceDto From(Invoice i) => new(
        i.Id, i.Number, i.IsCreditNote, i.LegalEntityId, i.PartyId, i.PartyName, i.InvoiceDate, i.DueDate, i.CurrencyCode, i.Net, i.Tax, i.Total, i.Paid, i.Status, i.SourceType, i.SourceId,
        i.JournalEntryId, i.Notes, i.Lines.OrderBy(l => l.Seq).Select(l => new InvoiceLineDto(l.Id, l.Seq, l.ItemId, l.Description, l.Quantity, l.Price, l.TaxCodeId, l.TaxAmount, l.Net, l.AccountId)).ToList(),
        i.CreditNoteForId, i.VoidReason, i.VoidedAt);
}

public sealed record InvoiceSummaryDto(Guid Id, string Number, bool IsCreditNote, Guid PartyId, string? PartyName, DateOnly InvoiceDate, DateOnly DueDate, decimal Total, decimal Paid, DocumentStatus Status, string CurrencyCode,
    decimal Net = 0, decimal Tax = 0, string? SourceType = null)
{
    public decimal Outstanding => Total - Paid;
}

public sealed record GetInvoice(Guid Id) : IQuery<InvoiceDto>, IRequirePermission { public string RequiredPermission => CorePermissions.InvoicesRead; }
public sealed record CreateInvoice(InvoiceInput Input) : ICommand<InvoiceDto>, IRequirePermission { public string RequiredPermission => CorePermissions.InvoicesWrite; }
public sealed record UpdateInvoice(Guid Id, InvoiceInput Input) : ICommand<InvoiceDto>, IRequirePermission { public string RequiredPermission => CorePermissions.InvoicesWrite; }
public sealed record DeleteInvoice(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => CorePermissions.InvoicesWrite; }
public sealed record IssueInvoice(Guid Id) : ICommand<InvoiceDto>, IRequirePermission { public string RequiredPermission => CorePermissions.InvoicesIssue; }
/// <summary>Reason is required (3-500 characters, see <see cref="VoidMemo"/>); it goes to the reversal memo and the audit log.</summary>
public sealed record VoidInvoice(Guid Id, string? Reason) : ICommand<InvoiceDto>, IRequirePermission { public string RequiredPermission => CorePermissions.InvoicesVoid; }

public sealed class VoidInvoiceValidator : AbstractValidator<VoidInvoice>
{
    public VoidInvoiceValidator() => RuleFor(x => x.Reason).ValidVoidReason();
}
/// <summary>Uses (part of) an issued credit note to settle an open invoice of the same customer.</summary>
public sealed record ApplyCreditNote(Guid CreditNoteId, Guid InvoiceId, decimal Amount) : ICommand<InvoiceDto>, IRequirePermission { public string RequiredPermission => CorePermissions.InvoicesCredit; }

/// <summary>One line to credit: an original invoice line and how much of its quantity to credit.</summary>
public sealed record CreditNoteLineInput(Guid InvoiceLineId, decimal Quantity);
/// <summary>
/// Drafts a credit note against an issued invoice: same customer and entity, the selected lines (all lines when none are given),
/// linked through SourceType "invoice" + SourceId and CreditNoteForId. The total of the invoice's non-voided credit notes cannot exceed the invoice total.
/// </summary>
public sealed record CreateCreditNoteFromInvoice(Guid InvoiceId, DateOnly? Date, List<CreditNoteLineInput>? Lines, string? Notes) : ICommand<InvoiceDto>, IRequirePermission
{
    public const string LinkType = "invoice";
    public string RequiredPermission => CorePermissions.InvoicesCredit;
}

/// <summary>Customer receipts allocated to one invoice (read-only; for the invoice detail view).</summary>
public sealed record InvoiceAllocationDto(Guid SettlementId, string Number, DateOnly Date, SettlementMethod Method, string? Reference, string CurrencyCode, decimal Amount, DocumentStatus Status);
public sealed record GetInvoiceAllocations(Guid InvoiceId) : IQuery<IReadOnlyList<InvoiceAllocationDto>>, IRequirePermission { public string RequiredPermission => CorePermissions.InvoicesRead; }

internal sealed class InvoiceAllocationHandler(IReadRepository<Settlement> settlements) : IRequestHandler<GetInvoiceAllocations, IReadOnlyList<InvoiceAllocationDto>>
{
    public async Task<IReadOnlyList<InvoiceAllocationDto>> Handle(GetInvoiceAllocations q, CancellationToken ct)
    {
        var rows = await settlements.Query(new Specification<Settlement>(s => s.Direction == InstrumentDirection.Received && s.Allocations.Any(a => a.InvoiceId == q.InvoiceId)).Include(s => s.Allocations))
            .OrderBy(s => s.Date).ToListAsync(ct);
        return rows.Select(s => new InvoiceAllocationDto(s.Id, s.Number, s.Date, s.Method, s.Reference, s.CurrencyCode,
            s.Allocations.Where(a => a.InvoiceId == q.InvoiceId).Sum(a => a.Amount), s.Status)).ToList();
    }
}

public sealed class InvoiceInputValidator : AbstractValidator<InvoiceInput>
{
    public InvoiceInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.PartyId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.Lines).NotNull().Must(l => l is { Count: > 0 }).WithMessage("An invoice needs at least one line.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.Description).NotEmpty().MaximumLength(400);
            l.RuleFor(x => x.Quantity).GreaterThan(0);
            l.RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        });
    }
}
public sealed class CreateInvoiceValidator : AbstractValidator<CreateInvoice> { public CreateInvoiceValidator() => RuleFor(x => x.Input).SetValidator(new InvoiceInputValidator()); }
public sealed class UpdateInvoiceValidator : AbstractValidator<UpdateInvoice> { public UpdateInvoiceValidator() => RuleFor(x => x.Input).SetValidator(new InvoiceInputValidator()); }
public sealed class ApplyCreditNoteValidator : AbstractValidator<ApplyCreditNote> { public ApplyCreditNoteValidator() => RuleFor(x => x.Amount).GreaterThan(0); }

internal sealed class InvoiceHandlers(IRepository<Invoice> invoices, IRepository<InvoiceLine> lineRows, IInvoicingService invoicing, IClock clock,
    IRepository<AuditEvent> audit, ICurrentUser user, IRepository<CreditApplication> creditApplications,
    IReadRepository<ERP.Domain.Organizations.LegalEntity> legalEntities, IReadRepository<ERP.Domain.Parties.Party> parties, ERP.Application.Templates.CountryPackRegistry countryPacks) :
    IRequestHandler<GetInvoice, InvoiceDto>,
    IRequestHandler<CreateInvoice, InvoiceDto>,
    IRequestHandler<UpdateInvoice, InvoiceDto>,
    IRequestHandler<DeleteInvoice, Unit>,
    IRequestHandler<IssueInvoice, InvoiceDto>,
    IRequestHandler<VoidInvoice, InvoiceDto>,
    IRequestHandler<ApplyCreditNote, InvoiceDto>,
    IRequestHandler<CreateCreditNoteFromInvoice, InvoiceDto>
{
    public async Task<InvoiceDto> Handle(GetInvoice q, CancellationToken ct)
        => InvoiceDto.From(await invoices.Query(new Specification<Invoice>(i => i.Id == q.Id).Include(i => i.Lines)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException());

    public async Task<InvoiceDto> Handle(CreateInvoice cmd, CancellationToken ct)
    {
        var invoice = await invoicing.BuildAsync(ToDraft(cmd.Input), ct);
        await invoices.SaveChangesAsync(ct);
        return InvoiceDto.From(invoice);
    }

    public async Task<InvoiceDto> Handle(UpdateInvoice cmd, CancellationToken ct)
    {
        var invoice = await LoadAsync(cmd.Id, ct);
        if (invoice.Status != DocumentStatus.Draft) throw new ConflictException("Only a draft can be edited. Void it or issue a credit note.");
        if (cmd.Input.LegalEntityId != invoice.LegalEntityId) throw new FieldValidationException("legalEntityId", "The legal entity of an invoice cannot change.");
        // Keep the link a module or a source invoice set (ToDraft has no source fields).
        var fresh = await invoicing.ApplyAsync(invoice, ToDraft(cmd.Input) with { SourceType = invoice.SourceType, SourceId = invoice.SourceId, BusinessUnitId = invoice.BusinessUnitId, CreditNoteForId = invoice.CreditNoteForId }, ct);
        await EnsureWithinUncreditedAsync(invoice, ct);
        foreach (var old in invoice.Lines.ToList()) lineRows.Remove(old);
        lineRows.AddRange(fresh);
        await invoices.SaveChangesAsync(ct);
        invoice.Lines.Clear(); invoice.Lines.AddRange(fresh);
        return InvoiceDto.From(invoice);
    }

    public async Task<Unit> Handle(DeleteInvoice cmd, CancellationToken ct)
    {
        var invoice = await LoadAsync(cmd.Id, ct);
        if (invoice.Status != DocumentStatus.Draft) throw new ConflictException("Only a draft can be deleted.");
        invoices.Remove(invoice);
        await invoices.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public async Task<InvoiceDto> Handle(IssueInvoice cmd, CancellationToken ct)
    {
        var invoice = await LoadAsync(cmd.Id, ct);
        await EnsureWithinUncreditedAsync(invoice, ct);
        await invoicing.IssueAsync(invoice, ct);
        await invoices.SaveChangesAsync(ct);
        return InvoiceDto.From(invoice) with { Warnings = await RequirementWarningsAsync(invoice, ct) };
    }

    /// <summary>What the country pack expects on a tax invoice and is missing; informational only.</summary>
    private async Task<IReadOnlyList<string>> RequirementWarningsAsync(Invoice invoice, CancellationToken ct)
    {
        var entity = await legalEntities.FindAsync(invoice.LegalEntityId, ct);
        if (entity is null) return [];
        var customer = await parties.FindAsync(invoice.PartyId, ct);
        return InvoiceRequirements.Warnings(countryPacks.Find(entity.CountryCode), entity, customer, invoice);
    }

    public async Task<InvoiceDto> Handle(VoidInvoice cmd, CancellationToken ct)
    {
        var invoice = await LoadAsync(cmd.Id, ct);
        await invoicing.VoidAsync(invoice, cmd.Reason, ct);
        VoidMemo.Audit(audit, user, clock, nameof(Invoice), invoice.Id, invoice.Number, cmd.Reason!);
        await invoices.SaveChangesAsync(ct);
        return InvoiceDto.From(invoice);
    }

    public async Task<InvoiceDto> Handle(ApplyCreditNote cmd, CancellationToken ct)
    {
        var credit = await LoadAsync(cmd.CreditNoteId, ct);
        var invoice = await LoadAsync(cmd.InvoiceId, ct);
        if (!credit.IsCreditNote || invoice.IsCreditNote) throw new ConflictException("Apply a credit note to an invoice.");
        if (credit.PartyId != invoice.PartyId || credit.LegalEntityId != invoice.LegalEntityId) throw new ConflictException("The credit note and the invoice must belong to the same customer and legal entity.");
        // A linked credit note applies to its own invoice while that invoice is still open. Once the original is fully
        // paid, what is left on the note is on-account credit for the customer and can go to any of their open invoices.
        if (credit.SourceType == CreateCreditNoteFromInvoice.LinkType && credit.SourceId is { } src && src != invoice.Id)
        {
            var sourceStatus = await invoices.Query(new Specification<Invoice>(i => i.Id == src)).Select(i => (DocumentStatus?)i.Status).FirstOrDefaultAsync(ct);
            if (sourceStatus != DocumentStatus.Paid)
                throw new ConflictException("This credit note was raised against another invoice and can only be applied to it while that invoice is still open.");
        }
        if (!IsOpen(credit) || !IsOpen(invoice)) throw new ConflictException("Both documents must be issued and not fully settled.");
        if (cmd.Amount > credit.Total - credit.Paid) throw new FieldValidationException("amount", "The amount exceeds what is left on the credit note.");
        if (cmd.Amount > invoice.Total - invoice.Paid) throw new FieldValidationException("amount", "The amount exceeds what is outstanding on the invoice.");
        // Both documents sit in receivables, so applying one to the other needs no new journal.
        credit.Paid += cmd.Amount; invoice.Paid += cmd.Amount;
        SettlementStatus.Refresh(credit); SettlementStatus.Refresh(invoice);
        CreditApplications.Record(creditApplications, credit, invoice, cmd.Amount, DateOnly.FromDateTime(clock.UtcNow), user.UserId);
        await invoices.SaveChangesAsync(ct);
        return InvoiceDto.From(invoice);
    }

    public async Task<InvoiceDto> Handle(CreateCreditNoteFromInvoice cmd, CancellationToken ct)
    {
        var source = await LoadAsync(cmd.InvoiceId, ct);
        if (source.IsCreditNote) throw new ConflictException("A credit note cannot be credited.");
        if (source.Status is not (DocumentStatus.Issued or DocumentStatus.PartlyPaid or DocumentStatus.Paid)) throw new ConflictException("Only an issued invoice can be credited.");
        var byId = source.Lines.ToDictionary(l => l.Id);
        var picked = cmd.Lines is { Count: > 0 } sel ? sel : source.Lines.Select(l => new CreditNoteLineInput(l.Id, l.Quantity)).ToList();
        var lines = new List<InvoiceLineDraft>();
        foreach (var p in picked)
        {
            if (!byId.TryGetValue(p.InvoiceLineId, out var l)) throw new FieldValidationException("lines", "A line does not belong to this invoice.");
            if (p.Quantity <= 0 || p.Quantity > l.Quantity) throw new FieldValidationException("lines", $"Quantity for '{l.Description}' must be between 0 and {l.Quantity}.");
            lines.Add(new InvoiceLineDraft(l.Description, p.Quantity, l.Price, l.TaxCodeId, l.AccountId, l.ItemId));
        }
        var date = cmd.Date ?? DateOnly.FromDateTime(clock.UtcNow);
        var credit = await invoicing.BuildAsync(new InvoiceDraft(source.LegalEntityId, source.PartyId, date, date, lines, IsCreditNote: true,
            SourceType: CreateCreditNoteFromInvoice.LinkType, SourceId: source.Id, BusinessUnitId: source.BusinessUnitId, Notes: cmd.Notes ?? $"Credit for {source.Number}",
            CreditNoteForId: source.Id), ct);
        await EnsureWithinUncreditedAsync(credit, ct);
        await invoices.SaveChangesAsync(ct);
        return InvoiceDto.From(credit);
    }

    /// <summary>For a credit note linked to an invoice: all of that invoice's non-voided credit notes together cannot exceed its total.</summary>
    private async Task EnsureWithinUncreditedAsync(Invoice credit, CancellationToken ct)
    {
        if (!credit.IsCreditNote || credit.SourceType != CreateCreditNoteFromInvoice.LinkType || credit.SourceId is not { } sourceId) return;
        var sourceTotal = await invoices.Query(new Specification<Invoice>(i => i.Id == sourceId)).Select(i => (decimal?)i.Total).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        var others = (await invoices.Query(new Specification<Invoice>(i => i.IsCreditNote && i.SourceType == CreateCreditNoteFromInvoice.LinkType && i.SourceId == sourceId
                && i.Id != credit.Id && i.Status != DocumentStatus.Voided)).Select(i => i.Total).ToListAsync(ct)).Sum(); // summed in memory: a handful of rows, and SQLite cannot SUM decimals
        var left = sourceTotal - others;
        if (credit.Total > left) throw new FieldValidationException("lines", $"The credit ({credit.Total:0.00}) exceeds the uncredited amount of the invoice ({left:0.00}).");
    }

    private static bool IsOpen(Invoice i) => i.Status is DocumentStatus.Issued or DocumentStatus.PartlyPaid;

    private async Task<Invoice> LoadAsync(Guid id, CancellationToken ct)
        => await invoices.Tracked(new Specification<Invoice>(i => i.Id == id).Include(i => i.Lines)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    private static InvoiceDraft ToDraft(InvoiceInput i)
        => new(i.LegalEntityId, i.PartyId, i.InvoiceDate, i.DueDate, i.Lines.Select(l => new InvoiceLineDraft(l.Description.Trim(), l.Quantity, l.Price, l.TaxCodeId, l.AccountId, l.ItemId)).ToList(), i.IsCreditNote, Notes: i.Notes);
}

public static class SettlementStatus
{
    /// <summary>Issued -> PartlyPaid -> Paid from the amount settled so far.</summary>
    public static void Refresh(Invoice i)
    {
        if (i.Status is DocumentStatus.Draft or DocumentStatus.Voided) return;
        i.Status = i.Paid <= 0 ? DocumentStatus.Issued : i.Paid >= i.Total ? DocumentStatus.Paid : DocumentStatus.PartlyPaid;
    }
}

public static class BillStatus
{
    /// <summary>Issued -> PartlyPaid -> Paid from the amount paid so far.</summary>
    public static void Refresh(Bill b)
    {
        if (b.Status is DocumentStatus.Draft or DocumentStatus.PendingApproval or DocumentStatus.Voided) return;
        b.Status = b.Paid <= 0 ? DocumentStatus.Issued : b.Paid >= b.Total ? DocumentStatus.Paid : DocumentStatus.PartlyPaid;
    }
}
