using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;
using MediatR;

namespace ERP.Application.Finance;

internal sealed class ApplyCreditNoteHandler(
    IInvoiceRepository invoices,
    IRepository<CreditApplication> creditApplications,
    ICurrentUser user,
    IClock clock,
    IPublisher publisher)
    : ICommandHandler<ApplyCreditNoteCommand, InvoiceDto>
{
    public async Task<InvoiceDto> Handle(ApplyCreditNoteCommand command, CancellationToken ct)
    {
        var credit = await invoices.GetWithLinesForUpdateAsync(command.CreditNoteId, ct);
        var invoice = await invoices.GetWithLinesForUpdateAsync(command.InvoiceId, ct);

        // 1. A credit note goes onto an invoice of the same customer and legal entity.
        if (!credit.IsCreditNote || invoice.IsCreditNote) throw new ConflictException("Apply a credit note to an invoice.");
        if (credit.PartyId != invoice.PartyId || credit.LegalEntityId != invoice.LegalEntityId)
            throw new ConflictException("The credit note and the invoice must belong to the same customer and legal entity.");

        // 2. A linked credit note applies to its own invoice while that invoice is still open. Once the original is fully
        //    paid, what is left on the note is on-account credit for the customer and can go to any of their open invoices.
        if (InvoiceCreditCaps.IsLinkedCreditNote(credit, out var originalId) && originalId != invoice.Id
            && await invoices.GetStatusAsync(originalId, ct) != DocumentStatus.Paid)
            throw new ConflictException("This credit note was raised against another invoice and can only be applied to it while that invoice is still open.");

        // 3. Both documents are open and the amount fits on both.
        if (!IsOpen(credit) || !IsOpen(invoice)) throw new ConflictException("Both documents must be issued and not fully settled.");
        if (command.Amount > credit.Total - credit.Paid) throw new FieldValidationException("amount", "The amount exceeds what is left on the credit note.");
        if (command.Amount > invoice.Total - invoice.Paid) throw new FieldValidationException("amount", "The amount exceeds what is outstanding on the invoice.");

        // 4. Both documents sit in receivables, so applying one to the other needs no new journal.
        credit.Paid += command.Amount;
        invoice.Paid += command.Amount;
        SettlementStatus.Refresh(credit);
        SettlementStatus.Refresh(invoice);
        CreditApplications.Record(creditApplications, credit, invoice, command.Amount, DateOnly.FromDateTime(clock.UtcNow), user.UserId);
        await invoices.SaveChangesAsync(ct);

        // 5. The module that raised the invoice learns it is now paid.
        if (invoice.SourceType != null && invoice.Status == DocumentStatus.Paid)
            await publisher.Publish(new InvoicePaymentChanged(invoice.Id, invoice.SourceType, invoice.SourceId, true), ct);

        return InvoiceDto.From(invoice);
    }

    private static bool IsOpen(Invoice i) => i.Status is DocumentStatus.Issued or DocumentStatus.PartlyPaid;
}
