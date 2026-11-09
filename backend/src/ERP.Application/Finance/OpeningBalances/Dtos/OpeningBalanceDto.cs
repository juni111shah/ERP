using ERP.Domain.Finance;

namespace ERP.Application.Finance;

public sealed record OpeningBalanceDto(
    Guid JournalId, string Number, Guid LegalEntityId, DateOnly Date, JournalStatus Status, decimal Amount, decimal OpeningEquityAmount,
    IReadOnlyList<Guid> InvoiceIds, IReadOnlyList<Guid> BillIds, Guid? ReversalJournalId = null);
