using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>Cheque / instrument details for receipt and payment DTOs (null when the settlement has no instrument).</summary>
public sealed record InstrumentInfo(string Kind, string No, DateOnly Date, string Status, string? BankName);

/// <summary>Status, amount and date of a received cheque (for the register summary).</summary>
public sealed record ChequeSummaryItem(InstrumentStatus Status, decimal Amount, DateOnly InstrumentDate);
