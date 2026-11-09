namespace ERP.Application.Finance;

// Narrow rows read from the posted ledger by IJournalEntryRepository. The handlers sum them on the client: decimal SUM is
// not portable across the supported databases.

/// <summary>One journal line: its account, the journal date, debit and credit.</summary>
public sealed record LedgerMovement(Guid AccountId, DateOnly Date, decimal Debit, decimal Credit);

/// <summary>One journal line: its account, debit and credit.</summary>
public sealed record AccountMovement(Guid AccountId, decimal Debit, decimal Credit);

/// <summary>The debit and credit of one journal line.</summary>
public sealed record LineAmounts(decimal Debit, decimal Credit);

/// <summary>One line of an account in the general ledger, with its journal.</summary>
public sealed record AccountLedgerLine(Guid JournalId, string Number, DateOnly Date, string? Memo, string? Description, decimal Debit, decimal Credit, int Seq);

/// <summary>One line on a cash account: the journal date and source, debit and credit.</summary>
public sealed record CashLine(DateOnly Date, string SourceType, decimal Debit, decimal Credit);

/// <summary>One line on a cash account, with the account.</summary>
public sealed record CashAccountLine(Guid AccountId, DateOnly Date, string SourceType, decimal Debit, decimal Credit);

/// <summary>One line of a journal, with the journal it belongs to.</summary>
public sealed record JournalLineAmounts(Guid JournalId, Guid AccountId, decimal Debit, decimal Credit);
