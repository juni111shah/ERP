using System.Linq.Expressions;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>Label is set only for an allocation with no linked invoice (InvoiceId = Guid.Empty, InvoiceNumber = "").</summary>
public sealed record AllocationDto(Guid Id, Guid InvoiceId, string InvoiceNumber, decimal Amount, string? Label = null);

public static class AllocationLabels
{
    public const string Unapplied = "Unapplied/On account";
}

/// <summary>A customer receipt with its allocations (the detail view).</summary>
public sealed record ReceiptDto(
    Guid Id, string Number, Guid LegalEntityId, Guid PartyId, string? PartyName, DateOnly Date, SettlementMethod Method, Guid? BankAccountId, string CurrencyCode, decimal Amount,
    decimal Unallocated, string? Reference, DocumentStatus Status, Guid? JournalEntryId, IReadOnlyList<AllocationDto> Allocations,
    Guid? PaymentInstrumentId = null, string? InstrumentKind = null, string? ChequeNumber = null, DateOnly? ChequeDate = null, string? ChequeStatus = null, string? ChequeBankName = null,
    string? VoidReason = null, DateTime? VoidedAt = null);

/// <summary>One row of the receipt list.</summary>
public sealed record ReceiptSummaryDto(Guid Id, string Number, Guid PartyId, string? PartyName, DateOnly Date, SettlementMethod Method, decimal Amount, decimal Unallocated, DocumentStatus Status, string CurrencyCode)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<Settlement, ReceiptSummaryDto>> Projection =
        r => new ReceiptSummaryDto(r.Id, r.Number, r.PartyId, r.PartyName, r.Date, r.Method, r.Amount, r.Unallocated, r.Status, r.CurrencyCode);
}
