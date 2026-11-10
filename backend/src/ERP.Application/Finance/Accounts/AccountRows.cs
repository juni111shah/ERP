using ERP.Domain.Finance;

namespace ERP.Application.Finance;

// Narrow rows of the chart of accounts read by IAccountRepository.

/// <summary>An account's code and name.</summary>
public sealed record AccountLabel(Guid Id, string Code, string Name);

/// <summary>An account's code, name and type.</summary>
public sealed record AccountHeader(Guid Id, string Code, string Name, AccountType Type);

/// <summary>An account's code and type.</summary>
public sealed record AccountCodeAndType(string Code, AccountType Type);

/// <summary>Whether an account can be posted to.</summary>
public sealed record AccountPostingState(Guid Id, string Code, bool IsPostable, bool IsActive);

/// <summary>What the ledger reports show of an account.</summary>
public sealed record AccountReportRow(Guid Id, string Code, string Name, AccountType Type, string? ReportingGroup);

/// <summary>What the cash flow statement needs to know of an account.</summary>
public sealed record CashFlowAccount(Guid Id, string Code, string Name, AccountType Type, string? ReportingGroup, string? SystemRole, bool IsBank);

/// <summary>An account's place in the tree.</summary>
public sealed record AccountLink(Guid Id, Guid? ParentId, AccountType Type, bool IsActive);

/// <summary>An account's place in the tree, with what its sub-category is derived from.</summary>
public sealed record AccountClassLink(Guid Id, Guid? ParentId, AccountType Type, bool IsActive, string Code, string? ReportingGroup, string? SystemRole);

/// <summary>What an account's sub-category is derived from.</summary>
public sealed record AccountClassRow(Guid Id, string Code, string? ReportingGroup, string? SystemRole);

/// <summary>A child account with its parent.</summary>
public sealed record AccountChildLink(Guid Id, Guid Parent, AccountType Type);
