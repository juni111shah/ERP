using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Panel 4 and deeper: the direct children of one account, paged / searchable like any list.</summary>
public sealed record ListAccountChildrenQuery : PagedQuery, IQuery<PagedResult<AccountNodeDto>>, IRequirePermission
{
    public Guid Id { get; init; }
    public bool? IncludeBalances { get; init; }
    public Guid? LegalEntityId { get; init; }
    public string RequiredPermission => CorePermissions.AccountsRead;
}
