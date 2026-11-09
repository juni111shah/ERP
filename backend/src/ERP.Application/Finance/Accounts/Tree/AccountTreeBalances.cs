using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>The optional balances of the chart-of-accounts panels: only when asked for, and only for callers who may read financial reports.</summary>
public sealed class AccountTreeBalances(IAccountRepository accounts, IJournalEntryRepository journals, ICurrentUser user)
{
    public bool Wanted(bool? requested) => requested == true && user.HasPermission(CorePermissions.ReportsRead);

    /// <summary>Posted movement (Dr − Cr) per account; the legal-entity filter of the repository always applies on top.</summary>
    public async Task<Dictionary<Guid, decimal>> MovementsAsync(IReadOnlyCollection<Guid>? accountIds, Guid? entityId, CancellationToken ct)
    {
        // Sum on the client: decimal SUM is not portable across the supported databases (same as the trial balance).
        var rows = await journals.ListPostedMovementsAsync(accountIds, entityId, ct);
        return rows.GroupBy(r => r.AccountId).ToDictionary(g => g.Key, g => g.Sum(r => r.Debit - r.Credit));
    }

    /// <summary>Each node's balance, rolled up over its same-type descendants.</summary>
    public async Task<PagedResult<AccountNodeDto>> WithBalancesAsync(PagedResult<AccountNodeDto> page, bool? requested, Guid? entityId, CancellationToken ct)
    {
        if (!Wanted(requested) || page.Items.Count == 0) return page;
        // Walk down level by level, asking only for the children of the accounts found so far (never the whole chart).
        // A child of another account type does not roll up into its parent: it is a chain of its own (see IsPrimary).
        var subtrees = page.Items.ToDictionary(i => i.Id, i => new HashSet<Guid> { i.Id });
        var typeOf = page.Items.ToDictionary(i => i.Id, i => i.Type);
        var owner = page.Items.ToDictionary(i => i.Id, i => i.Id); // account id -> the page item whose subtree it joined
        var frontier = page.Items.Select(i => i.Id).ToList();
        var seen = new HashSet<Guid>(frontier);
        while (frontier.Count > 0)
        {
            var level = frontier;
            var children = await accounts.ListChildLinksAsync(level, ct);
            frontier = [];
            foreach (var c in children)
            {
                var root = owner[c.Parent];
                if (c.Type != typeOf[root] || !seen.Add(c.Id)) continue;
                owner[c.Id] = root;
                subtrees[root].Add(c.Id);
                frontier.Add(c.Id);
            }
        }
        var sums = await MovementsAsync(subtrees.Values.SelectMany(s => s).Distinct().ToList(), entityId, ct);
        return page.Map(i => i with { Balance = AccountTreeRules.Natural(i.Type, subtrees[i.Id].Sum(id => sums.GetValueOrDefault(id))) });
    }
}
