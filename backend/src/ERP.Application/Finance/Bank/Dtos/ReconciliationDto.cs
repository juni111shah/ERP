namespace ERP.Application.Finance;

/// <summary>A ledger line on the bank account (positive = money in).</summary>
public sealed record LedgerItem(Guid JournalLineId, Guid JournalId, string Number, DateOnly Date, string? Description, decimal Amount);

/// <summary>The bank reconciliation of one account as of a date: what is unmatched on the statement and in the ledger.</summary>
public sealed record ReconciliationDto(
    Guid AccountId, string Code, string Name, DateOnly AsOf, decimal LedgerBalance, int Matched, IReadOnlyList<StatementLineDto> UnmatchedStatement, IReadOnlyList<LedgerItem> UnmatchedLedger,
    decimal UnmatchedStatementTotal, decimal UnmatchedLedgerTotal, bool Reconciled);
