using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>Part of a receipt (or received cheque) applied to one invoice.</summary>
public sealed record AllocationInput(Guid InvoiceId, decimal Amount);

/// <summary>The body of create receipt.</summary>
public sealed record ReceiptInput(Guid LegalEntityId, Guid PartyId, DateOnly Date, SettlementMethod Method, Guid BankAccountId, decimal Amount, string? Reference, List<AllocationInput>? Allocations, bool AutoAllocate = false);
