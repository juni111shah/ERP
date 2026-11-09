using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Panel 2: the sub-categories of one account type, always all of them in order, even when empty.</summary>
public sealed record GetAccountSubcategoriesQuery : IQuery<IReadOnlyList<AccountSubcategoryDto>>, IRequirePermission
{
    /// <summary>Account type, case-insensitive (asset, liability, equity, income, expense).</summary>
    public string? Type { get; init; }
    public bool? IncludeBalances { get; init; }
    public Guid? LegalEntityId { get; init; }
    public string RequiredPermission => CorePermissions.AccountsRead;
}
