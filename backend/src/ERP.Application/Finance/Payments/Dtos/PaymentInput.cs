using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>Part of a payment applied to one bill.</summary>
public sealed record BillAllocationInput(Guid BillId, decimal Amount);

/// <summary>The body of create payment. A cheque payment (<see cref="SettlementMethod.Instrument"/>) carries the cheque number, date and bank.</summary>
public sealed record PaymentInput(
    Guid LegalEntityId, Guid PartyId, DateOnly Date, SettlementMethod Method, Guid BankAccountId, decimal Amount, string? Reference, List<BillAllocationInput>? Allocations,
    string? ChequeNo = null, DateOnly? ChequeDate = null, string? BankName = null);
