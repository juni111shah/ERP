using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>
/// How much of an invoice its credit notes may credit. Used by update invoice, issue invoice and create credit note.
/// <list type="bullet">
/// <item>All the invoice's non-voided linked credit notes together cannot exceed the invoice total.</item>
/// <item>Per line: the quantity credited cannot exceed the quantity invoiced on that line.</item>
/// </list>
/// Credit notes raised before lines carried <see cref="InvoiceLine.SourceLineId"/> are covered only by the total cap.
/// </summary>
public sealed class InvoiceCreditCaps(IInvoiceRepository invoices)
{
    /// <summary>The <c>SourceType</c> of a credit note raised from an invoice (its <c>SourceId</c> is that invoice).</summary>
    public const string LinkType = "invoice";

    /// <summary>True when <paramref name="credit"/> is a credit note raised from an invoice; <paramref name="invoiceId"/> is that invoice.</summary>
    public static bool IsLinkedCreditNote(Invoice credit, out Guid invoiceId)
    {
        invoiceId = credit.SourceId ?? Guid.Empty;
        return credit.IsCreditNote && credit.SourceType == LinkType && credit.SourceId is not null;
    }

    /// <summary>Quantity already credited per line of an invoice, leaving out <paramref name="excludeCreditNoteId"/>.</summary>
    public Task<Dictionary<Guid, decimal>> CreditedPerLineAsync(Guid invoiceId, Guid? excludeCreditNoteId, CancellationToken ct)
        => invoices.CreditedQuantityPerLineAsync(invoiceId, excludeCreditNoteId, ct);

    /// <summary>Throws when <paramref name="credit"/> and the invoice's other credit notes would credit more than the invoice total.</summary>
    public async Task EnsureWithinUncreditedAsync(Invoice credit, CancellationToken ct)
    {
        if (!IsLinkedCreditNote(credit, out var invoiceId)) return;
        var invoiceTotal = await invoices.GetTotalAsync(invoiceId, ct) ?? throw new NotFoundException();
        var creditedByOthers = await invoices.TotalCreditedAsync(invoiceId, excludeCreditNoteId: credit.Id, ct);
        var left = invoiceTotal - creditedByOthers;
        if (credit.Total > left)
            throw new FieldValidationException("lines", $"The credit ({credit.Total:0.00}) exceeds the uncredited amount of the invoice ({left:0.00}).");
    }

    /// <summary>Throws when a line of draft credit note <paramref name="credit"/> credits more of an invoice line than is left on it.</summary>
    public async Task EnsureWithinLineCapsAsync(Invoice credit, IEnumerable<InvoiceLine> creditLines, CancellationToken ct)
    {
        if (!IsLinkedCreditNote(credit, out var invoiceId)) return;
        var mine = creditLines.Where(l => l.SourceLineId != null).GroupBy(l => l.SourceLineId!.Value).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
        if (mine.Count == 0) return;

        var creditedByOthers = await invoices.CreditedQuantityPerLineAsync(invoiceId, excludeCreditNoteId: credit.Id, ct);
        var invoiceLines = await invoices.GetLinesAsync(invoiceId, ct);
        foreach (var (lineId, quantity) in mine)
        {
            var line = invoiceLines.FirstOrDefault(l => l.Id == lineId);
            if (line is null) continue;
            var left = line.Quantity - creditedByOthers.GetValueOrDefault(lineId);
            if (quantity > left)
                throw new FieldValidationException("lines", $"'{line.Description}' has only {left} left to credit (already credited {line.Quantity - left} of {line.Quantity}).");
        }
    }
}
