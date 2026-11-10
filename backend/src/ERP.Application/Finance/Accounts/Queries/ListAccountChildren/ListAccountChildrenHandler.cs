using ERP.Application.Common;

namespace ERP.Application.Finance;

internal sealed class ListAccountChildrenHandler(IAccountRepository accounts, AccountTreeBalances balances, IAccountSubcategoryRules rules)
    : IQueryHandler<ListAccountChildrenQuery, PagedResult<AccountNodeDto>>
{
    public async Task<PagedResult<AccountNodeDto>> Handle(ListAccountChildrenQuery q, CancellationToken ct)
    {
        // Not found when the parent is outside the caller's workspace (tenant filter), the same as any other record.
        if (!await accounts.AnyAsync(a => a.Id == q.Id, ct)) throw new NotFoundException("The account was not found.");
        var page = await accounts.PageChildNodesAsync(q.Id, AccountTreeRules.Sorted(q), ct);
        return AccountTreeRules.WithSubcategories(await balances.WithBalancesAsync(page, q.IncludeBalances, q.LegalEntityId, ct), rules);
    }
}
