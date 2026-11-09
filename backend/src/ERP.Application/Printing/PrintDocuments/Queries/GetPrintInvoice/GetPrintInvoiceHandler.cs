using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Parties;

namespace ERP.Application.Printing;

internal sealed class GetPrintInvoiceHandler(IInvoiceRepository invoices, IReadRepository<Party> parties, PrintLookups lookups) : IQueryHandler<GetPrintInvoiceQuery, PrintInvoiceDto>
{
    public async Task<PrintInvoiceDto> Handle(GetPrintInvoiceQuery query, CancellationToken ct)
    {
        // 1. The invoice, the company header and the party.
        var invoice = await invoices.GetWithLinesAsync(query.Id, ct);
        var header = await lookups.BuildHeaderAsync(invoice.LegalEntityId, ct);
        var party = await parties.FindAsync(invoice.PartyId, ct);
        var partyAddress = party?.Address ?? string.Empty;

        // 2. Credit note raised from an invoice (linked via SourceType "invoice" + SourceId): print the original invoice.
        string? originalNumber = null;
        if (invoice.IsCreditNote && invoice.SourceType == InvoiceCreditCaps.LinkType && invoice.SourceId is { } srcId)
            originalNumber = await invoices.GetNumberAsync(srcId, ct);

        return new PrintInvoiceDto(
            header,
            // B-4: fall back to the party's current name before the print page shows "Unknown".
            InvoiceDto.From(invoice) with { PartyName = string.IsNullOrWhiteSpace(invoice.PartyName) ? party?.Name : invoice.PartyName },
            partyAddress,
            invoice.Lines.OrderBy(l => l.Seq).Select(l => new InvoiceLineDto(
                l.Id, l.Seq, l.ItemId, l.Description, l.Quantity, l.Price, l.TaxCodeId, l.TaxAmount, l.Net, l.AccountId)).ToList(), party?.TaxNumber, party?.Phone, party?.Email,
            originalNumber is null ? null : invoice.SourceId, originalNumber);
    }
}
