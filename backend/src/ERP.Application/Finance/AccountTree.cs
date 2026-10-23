using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Security;
using ERP.Domain.Finance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Finance;

// ---- Chart of accounts, one level at a time ------------------------------------------------------------------------
// The step-by-step chart-of-accounts screen opens one panel per level: account types first, then the primary accounts
// of one type, then the children of one account. Each request returns only that level plus a hasChildren flag, so the
// client never loads the whole chart at once. Balances are optional (includeBalances=true) and only returned to callers
// who may read financial reports; everyone else gets null.

/// <summary>One account type (category) with its counts and, when requested and allowed, its balance on the natural side.</summary>
public sealed record AccountCategoryDto(AccountType Type, int AccountCount, int ActiveCount, int PrimaryCount, bool HasChildren, decimal? Balance);

/// <summary>One account in a panel. <c>Balance</c> rolls up the account and all of its descendants, on the account type's natural side.</summary>
public sealed record AccountNodeDto(
    Guid Id, string Code, string Name, AccountType Type, Guid? ParentId, string? ReportingGroup, string? SystemRole,
    bool IsPostable, bool IsBank, bool IsSystem, bool IsActive, string? CurrencyCode, int ChildCount, bool HasChildren, decimal? Balance);

/// <summary>Panel 1: every account type, always in the same order, even when it has no accounts yet.</summary>
public sealed record GetAccountCategories : IQuery<IReadOnlyList<AccountCategoryDto>>, IRequirePermission
{
    public bool? IncludeBalances { get; init; }
    public Guid? LegalEntityId { get; init; }
    public string RequiredPermission => CorePermissions.AccountsRead;
}

/// <summary>
/// Panel 2: the primary accounts of one type, paged / searchable / sortable / filterable like any list. Primary means
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

/// <summary>Panel 3 and deeper: the direct children of one account, paged / searchable like any list.</summary>
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

internal sealed class AccountTreeHandlers(IReadRepository<Account> accounts, IReadRepository<JournalEntry> journals, ICurrentUser user, ICustomFieldService fields) :
    IRequestHandler<GetAccount, AccountDto>,
    IRequestHandler<GetAccountCategories, IReadOnlyList<AccountCategoryDto>>,
    IRequestHandler<ListCategoryAccounts, PagedResult<AccountNodeDto>>,
    IRequestHandler<ListAccountChildren, PagedResult<AccountNodeDto>>,
    IRequestHandler<GetAccountPath, IReadOnlyList<AccountNodeDto>>
{
    private sealed record Link(Guid Id, Guid? ParentId, AccountType Type, bool IsActive);

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
        if (!Enum.TryParse<AccountType>(q.Type, ignoreCase: true, out var type) || !Enum.IsDefined(type) || int.TryParse(q.Type, out _))
            throw new FieldValidationException("type", "Unknown account type. Use asset, liability, equity, income or expense.");
        var all = accounts.Query();
        var spec = new Specification<Account>(a => a.Type == type && (a.ParentId == null || !all.Any(p => p.Id == a.ParentId && p.Type == type)));
        var page = await accounts.PageAsync(Sorted(q), Projection(all), spec, ct);
        return await WithBalancesAsync(page, q.IncludeBalances, q.LegalEntityId, ct);
    }

    public async Task<PagedResult<AccountNodeDto>> Handle(ListAccountChildren q, CancellationToken ct)
    {
        // Not found when the parent is outside the caller's workspace (tenant filter), the same as any other record.
        if (!await accounts.AnyAsync(a => a.Id == q.Id, ct)) throw new NotFoundException("The account was not found.");
        var parentId = q.Id;
        var all = accounts.Query();
        var page = await accounts.PageAsync(Sorted(q), Projection(all), new Specification<Account>(a => a.ParentId == parentId), ct);
        return await WithBalancesAsync(page, q.IncludeBalances, q.LegalEntityId, ct);
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
            chain.Insert(0, node);
            current = node.ParentId;
        }
        return chain;
    }

    // ---- helpers ---------------------------------------------------------------------------------------------------

    private static PagedQuery Sorted(PagedQuery q) => string.IsNullOrWhiteSpace(q.Sort) ? q with { Sort = "code" } : q;

    private static System.Linq.Expressions.Expression<Func<Account, AccountNodeDto>> Projection(IQueryable<Account> all) => a => new AccountNodeDto(
        a.Id, a.Code, a.Name, a.Type, a.ParentId, a.ReportingGroup, a.SystemRole, a.IsPostable, a.IsBank, a.IsSystem, a.IsActive, a.CurrencyCode,
        all.Count(c => c.ParentId == a.Id), all.Any(c => c.ParentId == a.Id), null);

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
        var children = (await accounts.Query().Where(a => a.ParentId != null).Select(a => new { a.Id, Parent = a.ParentId!.Value }).ToListAsync(ct))
            .ToLookup(a => a.Parent, a => a.Id);
        var subtrees = page.Items.ToDictionary(i => i.Id, i =>
        {
            var set = new HashSet<Guid> { i.Id };
            var stack = new Stack<Guid>(); stack.Push(i.Id);
            while (stack.Count > 0) foreach (var c in children[stack.Pop()]) if (set.Add(c)) stack.Push(c);
            return set;
        });
        var sums = await MovementsAsync(subtrees.Values.SelectMany(s => s).Distinct().ToList(), entityId, ct);
        return page.Map(i => i with { Balance = Natural(i.Type, subtrees[i.Id].Sum(id => sums.GetValueOrDefault(id))) });
    }
}
