using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Security;
using ERP.Domain.Finance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Finance;

// ---- Chart of accounts, one level at a time ------------------------------------------------------------------------
// The step-by-step chart-of-accounts screen opens one panel per level: account types first, then the derived
// sub-categories of one type (see AccountSubcategories.cs), then the primary accounts of one sub-category, then the
// children of one account. Each request returns only that level plus a hasChildren flag, so the
// client never loads the whole chart at once. Balances are optional (includeBalances=true) and only returned to callers
// who may read financial reports; everyone else gets null.

/// <summary>One account type (category) with its counts and, when requested and allowed, its balance on the natural side.</summary>
public sealed record AccountCategoryDto(AccountType Type, int AccountCount, int ActiveCount, int PrimaryCount, bool HasChildren, decimal? Balance);

/// <summary>One account in a panel. <c>Balance</c> rolls up the account and all of its descendants, on the account type's natural side.</summary>
public sealed record AccountNodeDto(
    Guid Id, string Code, string Name, AccountType Type, Guid? ParentId, string? ReportingGroup, string? SystemRole,
    bool IsPostable, bool IsBank, bool IsSystem, bool IsActive, string? CurrencyCode, int ChildCount, bool HasChildren, decimal? Balance,
    string? Subcategory = null);

/// <summary>
/// One derived sub-category of an account type (Current assets, Non-current assets, ...). Counts cover every account of the
/// type whose top same-type ancestor falls in it; <c>PrimaryCount</c> is what its panel lists. Balance as for categories.
/// </summary>
public sealed record AccountSubcategoryDto(string Key, AccountType Type, string Name, int Order, int AccountCount, int ActiveCount, int PrimaryCount, bool HasChildren, decimal? Balance);

/// <summary>Panel 2: the sub-categories of one account type, always all of them in order, even when empty.</summary>
public sealed record GetAccountSubcategories : IQuery<IReadOnlyList<AccountSubcategoryDto>>, IRequirePermission
{
    /// <summary>Account type, case-insensitive (asset, liability, equity, income, expense).</summary>
    public string? Type { get; init; }
    public bool? IncludeBalances { get; init; }
    public Guid? LegalEntityId { get; init; }
    public string RequiredPermission => CorePermissions.AccountsRead;
}

/// <summary>Panel 3: the primary accounts of one sub-category, paged / searchable / sortable / filterable like any list.</summary>
public sealed record ListSubcategoryAccounts : PagedQuery, IQuery<PagedResult<AccountNodeDto>>, IRequirePermission
{
    public string? Type { get; init; }
    /// <summary>Sub-category key within the type, e.g. current (see GET .../tree/categories/{type}/subcategories).</summary>
    public string? Key { get; init; }
    public bool? IncludeBalances { get; init; }
    public Guid? LegalEntityId { get; init; }
    public string RequiredPermission => CorePermissions.AccountsRead;
}

/// <summary>Panel 1: every account type, always in the same order, even when it has no accounts yet.</summary>
public sealed record GetAccountCategories : IQuery<IReadOnlyList<AccountCategoryDto>>, IRequirePermission
{
    public bool? IncludeBalances { get; init; }
    public Guid? LegalEntityId { get; init; }
    public string RequiredPermission => CorePermissions.AccountsRead;
}

/// <summary>
/// All primary accounts of one type (kept for callers that skip the sub-category level), paged / searchable / sortable / filterable like any list. Primary means
/// no parent, or a parent of another type (so every account is reachable from its own category).
/// </summary>
public sealed record ListCategoryAccounts : PagedQuery, IQuery<PagedResult<AccountNodeDto>>, IRequirePermission
{
    /// <summary>Account type, case-insensitive (asset, liability, equity, income, expense).</summary>
    public string? Type { get; init; }
    public bool? IncludeBalances { get; init; }
    public Guid? LegalEntityId { get; init; }
    public string RequiredPermission => CorePermissions.AccountsRead;
}

/// <summary>Panel 4 and deeper: the direct children of one account, paged / searchable like any list.</summary>
public sealed record ListAccountChildren : PagedQuery, IQuery<PagedResult<AccountNodeDto>>, IRequirePermission
{
    public Guid Id { get; init; }
    public bool? IncludeBalances { get; init; }
    public Guid? LegalEntityId { get; init; }
    public string RequiredPermission => CorePermissions.AccountsRead;
}

/// <summary>The chain from the top-level ancestor down to the account itself, for breadcrumbs and re-opening a path after an edit.</summary>
public sealed record GetAccountPath(Guid Id) : IQuery<IReadOnlyList<AccountNodeDto>>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.AccountsRead;
}

/// <summary>One account with its custom fields (the step-by-step screen opens the edit drawer from a panel row).</summary>
public sealed record GetAccount(Guid Id) : IQuery<AccountDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.AccountsRead;
}

internal sealed class AccountTreeHandlers(
    IReadRepository<Account> accounts, IReadRepository<JournalEntry> journals, ICurrentUser user, ICustomFieldService fields, IAccountSubcategoryRules rules) :
    IRequestHandler<GetAccount, AccountDto>,
    IRequestHandler<GetAccountCategories, IReadOnlyList<AccountCategoryDto>>,
    IRequestHandler<GetAccountSubcategories, IReadOnlyList<AccountSubcategoryDto>>,
    IRequestHandler<ListSubcategoryAccounts, PagedResult<AccountNodeDto>>,
    IRequestHandler<ListCategoryAccounts, PagedResult<AccountNodeDto>>,
    IRequestHandler<ListAccountChildren, PagedResult<AccountNodeDto>>,
    IRequestHandler<GetAccountPath, IReadOnlyList<AccountNodeDto>>
{
    private sealed record Link(Guid Id, Guid? ParentId, AccountType Type, bool IsActive);
    private sealed record ClassLink(Guid Id, Guid? ParentId, AccountType Type, bool IsActive, string Code, string? ReportingGroup, string? SystemRole);

    public async Task<AccountDto> Handle(GetAccount q, CancellationToken ct)
    {
        var account = await accounts.GetAsync(q.Id, ct);
        return AccountDto.From(account, await fields.ReadAsync("account", account, ct));
    }

    public async Task<IReadOnlyList<AccountCategoryDto>> Handle(GetAccountCategories q, CancellationToken ct)
    {
        // Four narrow columns per account: cheap even for a large chart, and portable across databases.
        var links = await accounts.Query().Select(a => new Link(a.Id, a.ParentId, a.Type, a.IsActive)).ToListAsync(ct);
        var byId = links.ToDictionary(l => l.Id);
        Dictionary<AccountType, decimal>? balances = null;
        if (WantsBalances(q.IncludeBalances))
        {
            var sums = await MovementsAsync(null, q.LegalEntityId, ct);
            balances = links.GroupBy(l => l.Type).ToDictionary(g => g.Key, g => Natural(g.Key, g.Sum(l => sums.GetValueOrDefault(l.Id))));
        }
        return Enum.GetValues<AccountType>().Select(t =>
        {
            var ofType = links.Where(l => l.Type == t).ToList();
            var primary = ofType.Count(l => IsPrimary(l, byId));
            return new AccountCategoryDto(t, ofType.Count, ofType.Count(l => l.IsActive), primary, primary > 0,
                balances is null ? null : balances.GetValueOrDefault(t));
        }).ToList();
    }

    public async Task<PagedResult<AccountNodeDto>> Handle(ListCategoryAccounts q, CancellationToken ct)
    {
        var type = ParseType(q.Type);
        var all = accounts.Query();
        var spec = new Specification<Account>(a => a.Type == type && (a.ParentId == null || !all.Any(p => p.Id == a.ParentId && p.Type == type)));
        var page = await accounts.PageAsync(Sorted(q), Projection(all), spec, ct);
        return WithSubcategories(await WithBalancesAsync(page, q.IncludeBalances, q.LegalEntityId, ct));
    }

    public async Task<IReadOnlyList<AccountSubcategoryDto>> Handle(GetAccountSubcategories q, CancellationToken ct)
    {
        var type = ParseType(q.Type);
        // Narrow columns of every account: parents of another type decide what is primary, so the whole chart is needed.
        var links = await accounts.Query()
            .Select(a => new ClassLink(a.Id, a.ParentId, a.Type, a.IsActive, a.Code, a.ReportingGroup, a.SystemRole)).ToListAsync(ct);
        var byId = links.ToDictionary(l => l.Id);
        var ofType = links.Where(l => l.Type == type).Select(l =>
        {
            var top = TopOfChain(l, byId);
            return (Link: l, Primary: top.Id == l.Id, Sub: rules.Classify(type, top.Code, top.ReportingGroup, top.SystemRole));
        }).ToList();
        Dictionary<Guid, decimal>? sums = WantsBalances(q.IncludeBalances) ? await MovementsAsync(ofType.Select(x => x.Link.Id).ToList(), q.LegalEntityId, ct) : null;
        return rules.For(type).OrderBy(s => s.Order).Select(s =>
        {
            var inSub = ofType.Where(x => x.Sub == s.Key).ToList();
            var primary = inSub.Count(x => x.Primary);
            return new AccountSubcategoryDto(s.Key, type, s.Name, s.Order, inSub.Count, inSub.Count(x => x.Link.IsActive), primary, primary > 0,
                sums is null ? null : Natural(type, inSub.Sum(x => sums.GetValueOrDefault(x.Link.Id))));
        }).ToList();
    }

    public async Task<PagedResult<AccountNodeDto>> Handle(ListSubcategoryAccounts q, CancellationToken ct)
    {
        var type = ParseType(q.Type);
        var key = rules.For(type).FirstOrDefault(s => string.Equals(s.Key, q.Key, StringComparison.OrdinalIgnoreCase))?.Key
            ?? throw new FieldValidationException("key", $"Unknown sub-category for {type.ToString().ToLowerInvariant()}. Use {string.Join(", ", rules.For(type).Select(s => s.Key))}.");
        var all = accounts.Query();
        // Classification is code / reporting-group based (not translatable to SQL), so pick the ids first, then page them in the database.
        var primaries = await all.Where(a => a.Type == type && (a.ParentId == null || !all.Any(p => p.Id == a.ParentId && p.Type == type)))
            .Select(a => new { a.Id, a.Code, a.ReportingGroup, a.SystemRole }).ToListAsync(ct);
        var ids = primaries.Where(a => rules.Classify(type, a.Code, a.ReportingGroup, a.SystemRole) == key).Select(a => a.Id).ToList();
        var page = await accounts.PageAsync(Sorted(q), Projection(all), new Specification<Account>(a => ids.Contains(a.Id)), ct);
        return WithSubcategories(await WithBalancesAsync(page, q.IncludeBalances, q.LegalEntityId, ct));
    }

    public async Task<PagedResult<AccountNodeDto>> Handle(ListAccountChildren q, CancellationToken ct)
    {
        // Not found when the parent is outside the caller's workspace (tenant filter), the same as any other record.
        if (!await accounts.AnyAsync(a => a.Id == q.Id, ct)) throw new NotFoundException("The account was not found.");
        var parentId = q.Id;
        var all = accounts.Query();
        var page = await accounts.PageAsync(Sorted(q), Projection(all), new Specification<Account>(a => a.ParentId == parentId), ct);
        return WithSubcategories(await WithBalancesAsync(page, q.IncludeBalances, q.LegalEntityId, ct));
    }

    public async Task<IReadOnlyList<AccountNodeDto>> Handle(GetAccountPath q, CancellationToken ct)
    {
        var all = accounts.Query();
        var chain = new List<AccountNodeDto>();
        var seen = new HashSet<Guid>();
        for (Guid? current = q.Id; current is { } id && seen.Add(id);)
        {
            var node = await all.Where(a => a.Id == id).Select(Projection(all)).FirstOrDefaultAsync(ct);
            if (node is null)
            {
                if (chain.Count == 0) throw new NotFoundException("The account was not found.");
                break;
            }
            chain.Insert(0, node with { Subcategory = rules.Classify(node.Type, node.Code, node.ReportingGroup, node.SystemRole) });
            current = node.ParentId;
        }
        return chain;
    }

    // ---- helpers ---------------------------------------------------------------------------------------------------

    private static AccountType ParseType(string? value)
    {
        if (!Enum.TryParse<AccountType>(value, ignoreCase: true, out var type) || !Enum.IsDefined(type) || int.TryParse(value, out _))
            throw new FieldValidationException("type", "Unknown account type. Use asset, liability, equity, income or expense.");
        return type;
    }

    /// <summary>The account itself, or its highest ancestor of the same type (a parent of another type starts a new chain).</summary>
    private static ClassLink TopOfChain(ClassLink l, Dictionary<Guid, ClassLink> byId)
    {
        var top = l;
        var seen = new HashSet<Guid> { l.Id };
        while (top.ParentId is { } p && byId.TryGetValue(p, out var parent) && parent.Type == l.Type && seen.Add(parent.Id)) top = parent;
        return top;
    }

    /// <summary>Each node's own sub-category key (for a nested account, the panel path uses its top same-type ancestor's).</summary>
    private PagedResult<AccountNodeDto> WithSubcategories(PagedResult<AccountNodeDto> page)
        => page.Map(i => i with { Subcategory = rules.Classify(i.Type, i.Code, i.ReportingGroup, i.SystemRole) });

    private static PagedQuery Sorted(PagedQuery q) => string.IsNullOrWhiteSpace(q.Sort) ? q with { Sort = "code" } : q;

    private static System.Linq.Expressions.Expression<Func<Account, AccountNodeDto>> Projection(IQueryable<Account> all) => a => new AccountNodeDto(
        a.Id, a.Code, a.Name, a.Type, a.ParentId, a.ReportingGroup, a.SystemRole, a.IsPostable, a.IsBank, a.IsSystem, a.IsActive, a.CurrencyCode,
        all.Count(c => c.ParentId == a.Id), all.Any(c => c.ParentId == a.Id), null, null);

    private static bool IsPrimary(Link l, Dictionary<Guid, Link> byId)
        => l.ParentId is not { } p || !byId.TryGetValue(p, out var parent) || parent.Type != l.Type;

    private bool WantsBalances(bool? requested) => requested == true && user.HasPermission(CorePermissions.ReportsRead);

    /// <summary>Debit-natured types keep Dr − Cr; credit-natured types are shown positive on the credit side.</summary>
    private static decimal Natural(AccountType type, decimal debitMinusCredit)
        => type is AccountType.Asset or AccountType.Expense ? debitMinusCredit : -debitMinusCredit;

    /// <summary>Posted movement (Dr − Cr) per account; the legal-entity filter of the repository always applies on top.</summary>
    private async Task<Dictionary<Guid, decimal>> MovementsAsync(IReadOnlyCollection<Guid>? accountIds, Guid? entityId, CancellationToken ct)
    {
        var query = journals.Query(new Specification<JournalEntry>(j => j.Status != JournalStatus.Draft && (entityId == null || j.LegalEntityId == entityId)))
            .SelectMany(j => j.Lines, (j, l) => new { l.AccountId, l.Debit, l.Credit });
        if (accountIds is not null) query = query.Where(l => accountIds.Contains(l.AccountId));
        // Sum on the client: decimal SUM is not portable across the supported databases (same as the trial balance).
        var rows = await query.ToListAsync(ct);
        return rows.GroupBy(r => r.AccountId).ToDictionary(g => g.Key, g => g.Sum(r => r.Debit - r.Credit));
    }

    private async Task<PagedResult<AccountNodeDto>> WithBalancesAsync(PagedResult<AccountNodeDto> page, bool? requested, Guid? entityId, CancellationToken ct)
    {
        if (!WantsBalances(requested) || page.Items.Count == 0) return page;
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
            var children = await accounts.Query(new Specification<Account>(a => a.ParentId != null && level.Contains(a.ParentId.Value)))
                .Select(a => new { a.Id, Parent = a.ParentId!.Value, a.Type }).ToListAsync(ct);
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
        return page.Map(i => i with { Balance = Natural(i.Type, subtrees[i.Id].Sum(id => sums.GetValueOrDefault(id))) });
    }
}
