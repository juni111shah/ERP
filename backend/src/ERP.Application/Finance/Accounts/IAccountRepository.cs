using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>Data access for the chart of accounts. Implemented in ERP.Infrastructure/Repositories/Finance/AccountRepository.cs.</summary>
public interface IAccountRepository : IRepository<Account>
{
    /// <summary>The accounts with the given ids, by id.</summary>
    Task<Dictionary<Guid, Account>> MapByIdsAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>Code and name of the accounts with the given ids.</summary>
    Task<List<AccountLabel>> ListLabelsAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>Code, name and type of the accounts with the given ids, by id.</summary>
    Task<Dictionary<Guid, AccountHeader>> MapHeadersAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>The type of the accounts with the given ids, by id.</summary>
    Task<Dictionary<Guid, AccountType>> MapTypesAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>Whether the accounts with the given ids can be posted to.</summary>
    Task<List<AccountPostingState>> ListPostingStatesAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>The parent of an account (null for a top-level or unknown account).</summary>
    Task<Guid?> GetParentIdAsync(Guid id, CancellationToken ct);

    /// <summary>The code of an account; null when not found.</summary>
    Task<string?> GetCodeAsync(Guid id, CancellationToken ct);

    /// <summary>Code and type of an account; null when not found.</summary>
    Task<AccountCodeAndType?> FindCodeAndTypeAsync(Guid id, CancellationToken ct);

    /// <summary>Every account code that starts with <paramref name="prefix"/>.</summary>
    Task<List<string>> ListCodesStartingWithAsync(string prefix, CancellationToken ct);

    /// <summary>Every account, ordered by code, as the ledger reports show it.</summary>
    Task<List<AccountReportRow>> ListReportRowsByCodeAsync(CancellationToken ct);

    /// <summary>The ids of every income and expense account.</summary>
    Task<List<Guid>> ListIncomeAndExpenseIdsAsync(CancellationToken ct);

    /// <summary>The ids of every income and expense account, ordered by code.</summary>
    Task<List<Guid>> ListIncomeAndExpenseIdsByCodeAsync(CancellationToken ct);

    /// <summary>Code, name and type of the income and expense accounts among the given ids, ordered by code.</summary>
    Task<List<AccountHeader>> ListIncomeAndExpenseHeadersByCodeAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>Every account, active or not, with what the cash flow statement needs.</summary>
    Task<List<CashFlowAccount>> ListCashFlowChartAsync(CancellationToken ct);

    /// <summary>The active bank and cash accounts, ordered by code.</summary>
    Task<List<AccountLabel>> ListActiveCashAccountsAsync(CancellationToken ct);

    // ---- the step-by-step chart of accounts ----

    /// <summary>Every account's place in the tree.</summary>
    Task<List<AccountLink>> ListLinksAsync(CancellationToken ct);

    /// <summary>Every account's place in the tree, with what its sub-category is derived from.</summary>
    Task<List<AccountClassLink>> ListClassLinksAsync(CancellationToken ct);

    /// <summary>The primary accounts of a type (no parent, or a parent of another type), with what their sub-category is derived from.</summary>
    Task<List<AccountClassRow>> ListPrimaryClassRowsAsync(AccountType type, CancellationToken ct);

    /// <summary>A page of the primary accounts of a type, as tree nodes.</summary>
    Task<PagedResult<AccountNodeDto>> PagePrimaryNodesAsync(AccountType type, PagedQuery query, CancellationToken ct);

    /// <summary>A page of the accounts with the given ids, as tree nodes.</summary>
    Task<PagedResult<AccountNodeDto>> PageNodesAsync(List<Guid> ids, PagedQuery query, CancellationToken ct);

    /// <summary>A page of the direct children of an account, as tree nodes.</summary>
    Task<PagedResult<AccountNodeDto>> PageChildNodesAsync(Guid parentId, PagedQuery query, CancellationToken ct);

    /// <summary>One account as a tree node; null when not found.</summary>
    Task<AccountNodeDto?> FindNodeAsync(Guid id, CancellationToken ct);

    /// <summary>The direct children of the given accounts.</summary>
    Task<List<AccountChildLink>> ListChildLinksAsync(List<Guid> parentIds, CancellationToken ct);
    /// <summary>Those of the accounts that are active and postable, with their system role.</summary>
    Task<List<AccountRole>> ListPostableRolesAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>The active bank and cash accounts (asset accounts flagged as bank, or with the "cash" or "bank" role), by code.</summary>
    Task<List<CashAccount>> ListCashAndBankAsync(CancellationToken ct);

    /// <summary>Code and name by account id, for the ids that exist.</summary>
    Task<Dictionary<Guid, AccountLabel>> GetLabelsAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>How every account is classified (type, system role, bank flag, reporting group).</summary>
    Task<List<AccountClassification>> ListClassificationsAsync(CancellationToken ct);

    /// <summary>The code of every account.</summary>
    Task<List<string>> ListCodesAsync(CancellationToken ct);

    /// <summary>The system roles held by accounts (each account that has one).</summary>
    Task<List<string>> ListSystemRolesAsync(CancellationToken ct);

    /// <summary>The id of the active account holding the system role; null when there is none.</summary>
    Task<Guid?> FindActiveIdForRoleAsync(string systemRole, CancellationToken ct);
}

/// <summary>An account and its system role.</summary>
public sealed record AccountRole(Guid Id, string? SystemRole);

/// <summary>A bank or cash account.</summary>
public sealed record CashAccount(Guid Id, string Code, string Name, bool IsBank);

public sealed record AccountClassification(Guid Id, AccountType Type, string? SystemRole, bool IsBank, string? ReportingGroup);
