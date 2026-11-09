using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class GetAccountCategoriesHandler(IAccountRepository accounts, AccountTreeBalances balances)
    : IQueryHandler<GetAccountCategoriesQuery, IReadOnlyList<AccountCategoryDto>>
{
    public async Task<IReadOnlyList<AccountCategoryDto>> Handle(GetAccountCategoriesQuery q, CancellationToken ct)
    {
        // 1. Every account's place in the tree, and the balance per type when asked for.
        var links = await accounts.ListLinksAsync(ct);
        var byId = links.ToDictionary(l => l.Id);
        Dictionary<AccountType, decimal>? perType = null;
        if (balances.Wanted(q.IncludeBalances))
        {
            var sums = await balances.MovementsAsync(null, q.LegalEntityId, ct);
            perType = links.GroupBy(l => l.Type).ToDictionary(g => g.Key, g => AccountTreeRules.Natural(g.Key, g.Sum(l => sums.GetValueOrDefault(l.Id))));
        }

        // 2. One row per account type, in order.
        return Enum.GetValues<AccountType>().Select(t =>
        {
            var ofType = links.Where(l => l.Type == t).ToList();
            var primary = ofType.Count(l => IsPrimary(l, byId));
            return new AccountCategoryDto(t, ofType.Count, ofType.Count(l => l.IsActive), primary, primary > 0,
                perType is null ? null : perType.GetValueOrDefault(t));
        }).ToList();
    }

    private static bool IsPrimary(AccountLink l, Dictionary<Guid, AccountLink> byId)
        => l.ParentId is not { } p || !byId.TryGetValue(p, out var parent) || parent.Type != l.Type;
}
