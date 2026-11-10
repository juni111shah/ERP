using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>Builds the <see cref="ReceiptDto"/> every receipt command and query answers with (invoice numbers and cheque details looked up).</summary>
public sealed class ReceiptDtoBuilder(IInvoiceRepository invoices, IPaymentInstrumentRepository instruments)
{
    /// <summary>The receipt with <paramref name="allocations"/> (default: the receipt's loaded allocations).</summary>
    public async Task<ReceiptDto> BuildAsync(Settlement s, CancellationToken ct, IReadOnlyCollection<SettlementAllocation>? allocations = null)
    {
        var rows = allocations ?? s.Allocations;
        var ids = rows.Select(a => a.InvoiceId!.Value).ToList();
        var numbers = await invoices.NumbersAsync(ids, ct);
        var ins = await instruments.GetInfoAsync(s.PaymentInstrumentId, ct);
        return new ReceiptDto(s.Id, s.Number, s.LegalEntityId, s.PartyId, s.PartyName, s.Date, s.Method, s.BankAccountId, s.CurrencyCode, s.Amount, s.Unallocated, s.Reference, s.Status, s.JournalEntryId,
            rows.Select(a => new AllocationDto(a.Id, a.InvoiceId!.Value, numbers.GetValueOrDefault(a.InvoiceId.Value, "?"), a.Amount)).ToList(),
            s.PaymentInstrumentId, ins?.Kind, ins?.No, ins?.Date, ins?.Status, ins?.BankName, s.VoidReason, s.VoidedAt);
    }
}
