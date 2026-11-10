using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class CreateCreditNoteFromInvoiceHandler(
    IInvoiceRepository invoices,
    IInvoicingService invoicing,
    InvoiceCreditCaps creditCaps,
    IClock clock)
    : ICommandHandler<CreateCreditNoteFromInvoiceCommand, InvoiceDto>
{
    public async Task<InvoiceDto> Handle(CreateCreditNoteFromInvoiceCommand command, CancellationToken ct)
    {
        // 1. Only an issued invoice can be credited.
        var source = await invoices.GetWithLinesForUpdateAsync(command.InvoiceId, ct);
        if (source.IsCreditNote) throw new ConflictException("A credit note cannot be credited.");
        if (source.Status is not (DocumentStatus.Issued or DocumentStatus.PartlyPaid or DocumentStatus.Paid)) throw new ConflictException("Only an issued invoice can be credited.");

        // 2. The lines to credit: the ones asked for, or everything still uncredited on each line.
        var credited = await creditCaps.CreditedPerLineAsync(source.Id, excludeCreditNoteId: null, ct);
        var picked = command.Lines is { Count: > 0 } selected
            ? selected
            : source.Lines.Select(l => new CreditNoteLineInput(l.Id, l.Quantity - credited.GetValueOrDefault(l.Id))).Where(p => p.Quantity > 0).ToList();
        if (picked.Count == 0) throw new ConflictException("Every line of this invoice has already been credited.");

        // 3. Each picked quantity must fit within what is left on its invoice line.
        var byId = source.Lines.ToDictionary(l => l.Id);
        var lines = new List<InvoiceLineDraft>();
        foreach (var p in picked)
        {
            if (!byId.TryGetValue(p.InvoiceLineId, out var line)) throw new FieldValidationException("lines", "A line does not belong to this invoice.");
            var left = line.Quantity - credited.GetValueOrDefault(line.Id);
            if (p.Quantity <= 0 || p.Quantity > line.Quantity) throw new FieldValidationException("lines", $"Quantity for '{line.Description}' must be between 0 and {line.Quantity}.");
            if (p.Quantity > left) throw new FieldValidationException("lines", $"'{line.Description}' has only {left} left to credit (already credited {line.Quantity - left} of {line.Quantity}).");
            lines.Add(new InvoiceLineDraft(line.Description, p.Quantity, line.Price, line.TaxCodeId, line.AccountId, line.ItemId, SourceLineId: line.Id));
        }

        // 4. Build the draft credit note, check the total cap and save.
        var date = command.Date ?? DateOnly.FromDateTime(clock.UtcNow);
        var credit = await invoicing.BuildAsync(new InvoiceDraft(source.LegalEntityId, source.PartyId, date, date, lines, IsCreditNote: true,
            SourceType: InvoiceCreditCaps.LinkType, SourceId: source.Id, BusinessUnitId: source.BusinessUnitId, Notes: command.Notes ?? $"Credit for {source.Number}",
            CreditNoteForId: source.Id), ct);
        await creditCaps.EnsureWithinUncreditedAsync(credit, ct);
        await invoices.SaveChangesAsync(ct);
        return InvoiceDto.From(credit);
    }
}
