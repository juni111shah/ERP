using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>Builds the <see cref="PaymentDto"/> every payment command and query answers with (bill numbers and cheque details looked up).</summary>
public sealed class PaymentDtoBuilder(IBillRepository bills, IPaymentInstrumentRepository instruments)
{
    /// <summary>The payment with <paramref name="allocations"/> (default: the payment's loaded allocations).</summary>
    public async Task<PaymentDto> BuildAsync(Settlement s, CancellationToken ct, IReadOnlyCollection<SettlementAllocation>? allocations = null)
    {
        var rows = allocations ?? s.Allocations;
        var ids = rows.Select(a => a.BillId!.Value).ToList();
        var numbers = await bills.NumbersAsync(ids, ct);
        var ins = await instruments.GetInfoAsync(s.PaymentInstrumentId, ct);
        return new PaymentDto(s.Id, s.Number, s.LegalEntityId, s.PartyId, s.PartyName, s.Date, s.Method, s.BankAccountId, s.CurrencyCode, s.Amount, s.Unallocated, s.Reference, s.Status, s.PaymentInstrumentId, s.JournalEntryId,
            rows.Select(a => new BillAllocationDto(a.Id, a.BillId!.Value, numbers.GetValueOrDefault(a.BillId.Value, "?"), a.Amount)).ToList(),
            ins?.Kind, ins?.No, ins?.Date, ins?.Status, ins?.BankName, s.VoidReason, s.VoidedAt, s.ApprovalRequestId);
    }
}
