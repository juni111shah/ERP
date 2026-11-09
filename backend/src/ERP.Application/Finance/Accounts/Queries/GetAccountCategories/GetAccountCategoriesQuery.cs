using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Panel 1: every account type, always in the same order, even when it has no accounts yet.</summary>
public sealed record GetAccountCategoriesQuery : IQuery<IReadOnlyList<AccountCategoryDto>>, IRequirePermission
{
    public bool? IncludeBalances { get; init; }
    public Guid? LegalEntityId { get; init; }
    public string RequiredPermission => CorePermissions.AccountsRead;
}
