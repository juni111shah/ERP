using ERP.Application.Common;

namespace ERP.Application.Finance;

internal sealed class ListSubcategoryAccountsHandler(IAccountRepository accounts, AccountTreeBalances balances, IAccountSubcategoryRules rules)
    : IQueryHandler<ListSubcategoryAccountsQuery, PagedResult<AccountNodeDto>>
{
    public async Task<PagedResult<AccountNodeDto>> Handle(ListSubcategoryAccountsQuery q, CancellationToken ct)
    {
        // 1. A known sub-category of a known type.
        var type = AccountTreeRules.ParseType(q.Type);
        var key = rules.For(type).FirstOrDefault(s => string.Equals(s.Key, q.Key, StringComparison.OrdinalIgnoreCase))?.Key
            ?? throw new FieldValidationException("key", $"Unknown sub-category for {type.ToString().ToLowerInvariant()}. Use {string.Join(", ", rules.For(type).Select(s => s.Key))}.");

        // 2. Classification is code / reporting-group based (not translatable to SQL), so pick the ids first, then page them in the database.
        var primaries = await accounts.ListPrimaryClassRowsAsync(type, ct);
        var ids = primaries.Where(a => rules.Classify(type, a.Code, a.ReportingGroup, a.SystemRole) == key).Select(a => a.Id).ToList();
        var page = await accounts.PageNodesAsync(ids, AccountTreeRules.Sorted(q), ct);
        return AccountTreeRules.WithSubcategories(await balances.WithBalancesAsync(page, q.IncludeBalances, q.LegalEntityId, ct), rules);
    }
}
