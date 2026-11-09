using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Panel 3: the primary accounts of one sub-category, paged / searchable / sortable / filterable like any list.</summary>
public sealed record ListSubcategoryAccountsQuery : PagedQuery, IQuery<PagedResult<AccountNodeDto>>, IRequirePermission
{
    public string? Type { get; init; }
    /// <summary>Sub-category key within the type, e.g. current (see GET .../tree/categories/{type}/subcategories).</summary>
    public string? Key { get; init; }
    public bool? IncludeBalances { get; init; }
    public Guid? LegalEntityId { get; init; }
    public string RequiredPermission => CorePermissions.AccountsRead;
}
