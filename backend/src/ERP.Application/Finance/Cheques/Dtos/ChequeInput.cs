using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>The body of register cheque and of replace cheque (the replacement): a cheque received from a customer and the invoices it pays.</summary>
public sealed record ChequeInput(
    Guid LegalEntityId, Guid PartyId, InstrumentKind Kind, string InstrumentNo, string? BankName, decimal Amount, DateOnly InstrumentDate, DateOnly? ReceivedOn, string? Notes,
    List<AllocationInput>? Allocations, bool AutoAllocate = false);
