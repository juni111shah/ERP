using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>
/// All primary accounts of one type (kept for callers that skip the sub-category level), paged / searchable / sortable / filterable like any list. Primary means
/// no parent, or a parent of another type (so every account is reachable from its own category).
/// </summary>
public sealed record ListCategoryAccountsQuery : PagedQuery, IQuery<PagedResult<AccountNodeDto>>, IRequirePermission
{
    /// <summary>Account type, case-insensitive (asset, liability, equity, income, expense).</summary>
    public string? Type { get; init; }
    public bool? IncludeBalances { get; init; }
    public Guid? LegalEntityId { get; init; }
    public string RequiredPermission => CorePermissions.AccountsRead;
}
