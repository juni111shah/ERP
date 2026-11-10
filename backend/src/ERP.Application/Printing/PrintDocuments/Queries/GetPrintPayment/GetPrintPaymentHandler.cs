using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Parties;

namespace ERP.Application.Printing;

internal sealed class GetPrintPaymentHandler(ISettlementRepository settlements, IBillRepository bills, IReadRepository<Party> parties, PrintLookups lookups)
    : IQueryHandler<GetPrintPaymentQuery, PrintPaymentDto>
{
    public async Task<PrintPaymentDto> Handle(GetPrintPaymentQuery query, CancellationToken ct)
    {
        // 1. The payment, the company header and the vendor.
        var payment = await settlements.GetPaymentWithAllocationsAsync(query.Id, ct);
        var header = await lookups.BuildHeaderAsync(payment.LegalEntityId, ct);
        var party = await parties.FindAsync(payment.PartyId, ct);

        // 2. The allocations with the numbers of the bills they settle.
        var ids = payment.Allocations.Where(a => a.BillId.HasValue).Select(a => a.BillId!.Value).ToList();
        var billNumbers = await bills.GetNumbersAsync(ids, ct);
        // Unlinked allocations are kept (blank number, "Unapplied/On account") so the lines add up to the payment.
        var allocations = payment.Allocations.Select(a => a.BillId is { } billId
            ? new BillAllocationDto(a.Id, billId, billNumbers.GetValueOrDefault(billId, "?"), a.Amount)
            : new BillAllocationDto(a.Id, Guid.Empty, "", a.Amount, AllocationLabels.Unapplied)).ToList();

        var paymentDto = new PaymentDto(
            payment.Id, payment.Number, payment.LegalEntityId, payment.PartyId, payment.PartyName, payment.Date,
            payment.Method, payment.BankAccountId, payment.CurrencyCode, payment.Amount, payment.Unallocated,
            payment.Reference, payment.Status, payment.PaymentInstrumentId, payment.JournalEntryId, allocations);

        // 3. The cheque or other instrument it was paid with, if any.
        var ins = await lookups.InstrumentAsync(payment.PaymentInstrumentId, ct);

        return new PrintPaymentDto(
            header,
            paymentDto,
            party?.Name ?? payment.PartyName ?? "Unknown",
            allocations, party?.Address, party?.TaxNumber, party?.Phone, party?.Email,
            ins?.Kind, ins?.No, ins?.Date, ins?.Status, ins?.BankName);
    }
}
