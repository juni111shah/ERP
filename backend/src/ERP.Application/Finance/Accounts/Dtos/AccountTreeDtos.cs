using ERP.Domain.Finance;

namespace ERP.Application.Finance;

// ---- Chart of accounts, one level at a time ------------------------------------------------------------------------
// The step-by-step chart-of-accounts screen opens one panel per level: account types first, then the derived
// sub-categories of one type (see AccountSubcategories.cs), then the primary accounts of one sub-category, then the
// children of one account. Each request returns only that level plus a hasChildren flag, so the
// client never loads the whole chart at once. Balances are optional (includeBalances=true) and only returned to callers
// who may read financial reports; everyone else gets null.

/// <summary>One account type (category) with its counts and, when requested and allowed, its balance on the natural side.</summary>
public sealed record AccountCategoryDto(AccountType Type, int AccountCount, int ActiveCount, int PrimaryCount, bool HasChildren, decimal? Balance);

/// <summary>One account in a panel. <c>Balance</c> rolls up the account and all of its descendants, on the account type's natural side.</summary>
public sealed record AccountNodeDto(
    Guid Id, string Code, string Name, AccountType Type, Guid? ParentId, string? ReportingGroup, string? SystemRole,
    bool IsPostable, bool IsBank, bool IsSystem, bool IsActive, string? CurrencyCode, int ChildCount, bool HasChildren, decimal? Balance,
    string? Subcategory = null);

/// <summary>
/// One derived sub-category of an account type (Current assets, Non-current assets, ...). Counts cover every account of the
/// type whose top same-type ancestor falls in it; <c>PrimaryCount</c> is what its panel lists. Balance as for categories.
/// </summary>
public sealed record AccountSubcategoryDto(string Key, AccountType Type, string Name, int Order, int AccountCount, int ActiveCount, int PrimaryCount, bool HasChildren, decimal? Balance);
