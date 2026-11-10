using ERP.Application.Common;

namespace ERP.Application.Finance;

internal sealed class GetAccountSubcategoriesHandler(IAccountRepository accounts, AccountTreeBalances balances, IAccountSubcategoryRules rules)
    : IQueryHandler<GetAccountSubcategoriesQuery, IReadOnlyList<AccountSubcategoryDto>>
{
    public async Task<IReadOnlyList<AccountSubcategoryDto>> Handle(GetAccountSubcategoriesQuery q, CancellationToken ct)
    {
        // 1. Narrow columns of every account: parents of another type decide what is primary, so the whole chart is needed.
        var type = AccountTreeRules.ParseType(q.Type);
        var links = await accounts.ListClassLinksAsync(ct);
        var byId = links.ToDictionary(l => l.Id);

        // 2. Each account of the type falls in the sub-category of its top same-type ancestor.
        var ofType = links.Where(l => l.Type == type).Select(l =>
        {
            var top = TopOfChain(l, byId);
            return (Link: l, Primary: top.Id == l.Id, Sub: rules.Classify(type, top.Code, top.ReportingGroup, top.SystemRole));
        }).ToList();
        Dictionary<Guid, decimal>? sums = balances.Wanted(q.IncludeBalances) ? await balances.MovementsAsync(ofType.Select(x => x.Link.Id).ToList(), q.LegalEntityId, ct) : null;

        // 3. One row per sub-category, in order.
        return rules.For(type).OrderBy(s => s.Order).Select(s =>
        {
            var inSub = ofType.Where(x => x.Sub == s.Key).ToList();
            var primary = inSub.Count(x => x.Primary);
            return new AccountSubcategoryDto(s.Key, type, s.Name, s.Order, inSub.Count, inSub.Count(x => x.Link.IsActive), primary, primary > 0,
                sums is null ? null : AccountTreeRules.Natural(type, inSub.Sum(x => sums.GetValueOrDefault(x.Link.Id))));
        }).ToList();
    }

    /// <summary>The account itself, or its highest ancestor of the same type (a parent of another type starts a new chain).</summary>
    private static AccountClassLink TopOfChain(AccountClassLink l, Dictionary<Guid, AccountClassLink> byId)
    {
        var top = l;
        var seen = new HashSet<Guid> { l.Id };
        while (top.ParentId is { } p && byId.TryGetValue(p, out var parent) && parent.Type == l.Type && seen.Add(parent.Id)) top = parent;
        return top;
    }
}
