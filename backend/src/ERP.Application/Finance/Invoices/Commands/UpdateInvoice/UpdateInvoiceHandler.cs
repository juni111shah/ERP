using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class UpdateInvoiceHandler(
    IInvoiceRepository invoices,
    IRepository<InvoiceLine> invoiceLines,
    IInvoicingService invoicing,
    InvoiceCreditCaps creditCaps)
    : ICommandHandler<UpdateInvoiceCommand, InvoiceDto>
{
    public async Task<InvoiceDto> Handle(UpdateInvoiceCommand command, CancellationToken ct)
    {
        // 1. Only a draft can change, and never its legal entity.
        var invoice = await invoices.GetWithLinesForUpdateAsync(command.Id, ct);
        if (invoice.Status != DocumentStatus.Draft) throw new ConflictException("Only a draft can be edited. Void it or issue a credit note.");
        if (command.Input.LegalEntityId != invoice.LegalEntityId) throw new FieldValidationException("legalEntityId", "The legal entity of an invoice cannot change.");

        // 2. Re-price from the new input, keeping the link a module or a source invoice set (the input has no source fields).
        var draft = command.Input.ToDraft() with
        {
            SourceType = invoice.SourceType, SourceId = invoice.SourceId, BusinessUnitId = invoice.BusinessUnitId, CreditNoteForId = invoice.CreditNoteForId,
        };
        var newLines = await invoicing.ApplyAsync(invoice, draft, ct);
        await creditCaps.EnsureWithinUncreditedAsync(invoice, ct);

        // 3. A draft credit note keeps pointing each line at the invoice line it reverses, so editing cannot get around the per-line cap.
        var oldLines = invoice.Lines.OrderBy(l => l.Seq).ToList();
        for (var n = 0; n < newLines.Count && n < oldLines.Count; n++)
            if (oldLines[n].SourceLineId != null && oldLines[n].Description == newLines[n].Description) newLines[n].SourceLineId = oldLines[n].SourceLineId;
        await creditCaps.EnsureWithinLineCapsAsync(invoice, newLines, ct);

        // 4. Swap the lines and save.
        foreach (var old in invoice.Lines.ToList()) invoiceLines.Remove(old);
        invoiceLines.AddRange(newLines);
        await invoices.SaveChangesAsync(ct);

        invoice.Lines.Clear();
        invoice.Lines.AddRange(newLines);
        return InvoiceDto.From(invoice);
    }
}
