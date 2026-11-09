using System.Linq.Expressions;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>Label is set only for an allocation with no linked bill (BillId = Guid.Empty, BillNumber = "").</summary>
public sealed record BillAllocationDto(Guid Id, Guid BillId, string BillNumber, decimal Amount, string? Label = null);

/// <summary>A vendor payment with its allocations (the detail view).</summary>
public sealed record PaymentDto(
    Guid Id, string Number, Guid LegalEntityId, Guid PartyId, string? PartyName, DateOnly Date, SettlementMethod Method, Guid? BankAccountId, string CurrencyCode, decimal Amount, decimal Unallocated,
    string? Reference, DocumentStatus Status, Guid? PaymentInstrumentId, Guid? JournalEntryId, IReadOnlyList<BillAllocationDto> Allocations,
    string? InstrumentKind = null, string? ChequeNumber = null, DateOnly? ChequeDate = null, string? ChequeStatus = null, string? ChequeBankName = null,
    string? VoidReason = null, DateTime? VoidedAt = null, Guid? ApprovalRequestId = null);

/// <summary>One row of the payment list.</summary>
public sealed record PaymentSummaryDto(Guid Id, string Number, Guid PartyId, string? PartyName, DateOnly Date, SettlementMethod Method, decimal Amount, decimal Unallocated, DocumentStatus Status, string CurrencyCode)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<Settlement, PaymentSummaryDto>> Projection =
        p => new PaymentSummaryDto(p.Id, p.Number, p.PartyId, p.PartyName, p.Date, p.Method, p.Amount, p.Unallocated, p.Status, p.CurrencyCode);
}
