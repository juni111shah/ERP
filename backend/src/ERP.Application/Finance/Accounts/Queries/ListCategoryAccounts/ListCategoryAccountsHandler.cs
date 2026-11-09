using ERP.Application.Common;

namespace ERP.Application.Finance;

internal sealed class ListCategoryAccountsHandler(IAccountRepository accounts, AccountTreeBalances balances, IAccountSubcategoryRules rules)
    : IQueryHandler<ListCategoryAccountsQuery, PagedResult<AccountNodeDto>>
{
    public async Task<PagedResult<AccountNodeDto>> Handle(ListCategoryAccountsQuery q, CancellationToken ct)
    {
        var type = AccountTreeRules.ParseType(q.Type);
        var page = await accounts.PagePrimaryNodesAsync(type, AccountTreeRules.Sorted(q), ct);
        return AccountTreeRules.WithSubcategories(await balances.WithBalancesAsync(page, q.IncludeBalances, q.LegalEntityId, ct), rules);
    }
}
