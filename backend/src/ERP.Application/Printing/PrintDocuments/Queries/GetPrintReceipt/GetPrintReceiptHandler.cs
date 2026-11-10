using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Parties;

namespace ERP.Application.Printing;

internal sealed class GetPrintReceiptHandler(ISettlementRepository settlements, IInvoiceRepository invoices, IReadRepository<Party> parties, PrintLookups lookups)
    : IQueryHandler<GetPrintReceiptQuery, PrintReceiptDto>
{
    public async Task<PrintReceiptDto> Handle(GetPrintReceiptQuery query, CancellationToken ct)
    {
        // 1. The receipt, the company header and the party.
        var receipt = await settlements.GetReceiptWithAllocationsAsync(query.Id, ct);
        var header = await lookups.BuildHeaderAsync(receipt.LegalEntityId, ct);
        var party = await parties.FindAsync(receipt.PartyId, ct);

        // 2. The allocations with the numbers of the invoices they settle.
        var ids = receipt.Allocations.Where(a => a.InvoiceId.HasValue).Select(a => a.InvoiceId!.Value).ToList();
        var invoiceNumbers = await invoices.GetNumbersAsync(ids, ct);
        // Unlinked allocations are kept (blank number, "Unapplied/On account") so the lines add up to the receipt.
        var allocations = receipt.Allocations.Select(a => a.InvoiceId is { } invId
            ? new AllocationDto(a.Id, invId, invoiceNumbers.GetValueOrDefault(invId, "?"), a.Amount)
            : new AllocationDto(a.Id, Guid.Empty, "", a.Amount, AllocationLabels.Unapplied)).ToList();

        var receiptDto = new ReceiptDto(
            receipt.Id, receipt.Number, receipt.LegalEntityId, receipt.PartyId, receipt.PartyName, receipt.Date,
            receipt.Method, receipt.BankAccountId, receipt.CurrencyCode, receipt.Amount, receipt.Unallocated,
            receipt.Reference, receipt.Status, receipt.JournalEntryId, allocations);

        // 3. The cheque or other instrument it was received with, if any.
        var ins = await lookups.InstrumentAsync(receipt.PaymentInstrumentId, ct);

        return new PrintReceiptDto(
            header,
            receiptDto,
            party?.Name ?? receipt.PartyName ?? "Unknown",
            party?.Address,
            allocations, party?.TaxNumber, party?.Phone, party?.Email,
            ins?.Kind, ins?.No, ins?.Date, ins?.Status, ins?.BankName);
    }
}
