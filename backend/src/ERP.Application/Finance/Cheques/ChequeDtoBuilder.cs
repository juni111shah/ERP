using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>Builds the <see cref="ChequeDto"/> every cheque command and query answers with (allocations, document numbers and the replacing cheque looked up).</summary>
public sealed class ChequeDtoBuilder(
    ISettlementAllocationRepository allocationRows,
    IInvoiceRepository invoices,
    IBillRepository bills,
    IPaymentInstrumentRepository instruments)
{
    public async Task<ChequeDto> BuildAsync(PaymentInstrument c, CancellationToken ct)
    {
        var allocations = new List<AllocationDto>();
        if (c.SettlementId is { } sid)
        {
            var rows = await allocationRows.ListForSettlementAsync(sid, ct);
            var invoiceIds = rows.Where(a => a.InvoiceId != null).Select(a => a.InvoiceId!.Value).ToList();
            var billIds = rows.Where(a => a.BillId != null).Select(a => a.BillId!.Value).ToList();
            var numbers = await invoices.NumbersAsync(invoiceIds, ct);
            foreach (var (billId, billNumber) in await bills.NumbersAsync(billIds, ct)) numbers[billId] = billNumber;
            allocations = rows.Select(a => { var id = (a.InvoiceId ?? a.BillId)!.Value; return new AllocationDto(a.Id, id, numbers.GetValueOrDefault(id, "?"), a.Amount); }).ToList();
        }
        var replacedBy = await instruments.GetReplacementIdAsync(c.Id, ct);
        return new ChequeDto(c.Id, c.Number, c.LegalEntityId, c.Kind, c.Direction, c.InstrumentNo, c.BankName, c.Amount, c.CurrencyCode, c.InstrumentDate, c.ReceivedOn, c.PartyId, c.PartyName, c.Status,
            c.BankAccountId, c.SettlementId, c.DepositedOn, c.ClearedOn, c.BounceReason, c.ReplacesInstrumentId, c.Notes, allocations, c.BouncedOn, c.FollowUpOn, c.FollowUpNote, replacedBy);
    }
}
