using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>The chain from the top-level ancestor down to the account itself, for breadcrumbs and re-opening a path after an edit.</summary>
public sealed record GetAccountPathQuery(Guid Id) : IQuery<IReadOnlyList<AccountNodeDto>>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.AccountsRead;
}
