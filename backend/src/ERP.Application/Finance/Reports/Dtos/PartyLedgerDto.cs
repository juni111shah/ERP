namespace ERP.Application.Finance;

/// <summary>
/// One posted document on a party's account. <see cref="Debit"/> raises what the customer owes (invoice) or lowers what is owed to the
/// vendor (payment); <see cref="Credit"/> does the opposite (credit note, receipt / bill). <see cref="Balance"/> is the running balance after it.
/// </summary>
public sealed record PartyLedgerLine(DateOnly Date, string Kind, Guid DocumentId, string Number, string? Reference, string CurrencyCode, decimal Debit, decimal Credit, decimal Balance);

/// <summary>
/// Statement of account for a date range: opening balance (everything posted before <see cref="From"/>), each document in the range with
/// a running balance, and the closing balance. Customer balances are receivable (positive = the customer owes), vendor balances are
/// payable (positive = owed to the vendor), the same signs as the open-item statements. Draft, pending and voided documents are left out.
/// </summary>
public sealed record PartyLedgerDto(Guid PartyId, string? PartyName, string Side, DateOnly From, DateOnly To, decimal OpeningBalance,
    IReadOnlyList<PartyLedgerLine> Lines, decimal TotalDebit, decimal TotalCredit, decimal ClosingBalance);
