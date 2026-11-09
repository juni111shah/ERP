using System.Linq.Expressions;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Finance;

internal sealed class AccountRepository(IRepository<Account> accounts) : RepositoryBase<Account>(accounts), IAccountRepository
{
    public Task<Dictionary<Guid, Account>> MapByIdsAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Account>(a => ids.Contains(a.Id))).ToDictionaryAsync(a => a.Id, ct);

    public Task<List<AccountLabel>> ListLabelsAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Account>(a => ids.Contains(a.Id))).Select(a => new AccountLabel(a.Id, a.Code, a.Name)).ToListAsync(ct);

    public Task<Dictionary<Guid, AccountHeader>> MapHeadersAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Account>(a => ids.Contains(a.Id))).Select(a => new AccountHeader(a.Id, a.Code, a.Name, a.Type)).ToDictionaryAsync(a => a.Id, ct);

    public Task<Dictionary<Guid, AccountType>> MapTypesAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Account>(a => ids.Contains(a.Id))).Select(a => new { a.Id, a.Type }).ToDictionaryAsync(a => a.Id, a => a.Type, ct);

    public Task<List<AccountPostingState>> ListPostingStatesAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Account>(a => ids.Contains(a.Id))).Select(a => new AccountPostingState(a.Id, a.Code, a.IsPostable, a.IsActive)).ToListAsync(ct);

    public Task<Guid?> GetParentIdAsync(Guid id, CancellationToken ct)
        => Query(new Specification<Account>(a => a.Id == id)).Select(a => a.ParentId).FirstOrDefaultAsync(ct);

    public Task<string?> GetCodeAsync(Guid id, CancellationToken ct)
        => Query(new Specification<Account>(a => a.Id == id)).Select(a => a.Code).FirstOrDefaultAsync(ct);

    public async Task<AccountCodeAndType?> FindCodeAndTypeAsync(Guid id, CancellationToken ct)
    {
        var row = await Query(new Specification<Account>(a => a.Id == id)).Select(a => new { a.Code, a.Type }).FirstOrDefaultAsync(ct);
        return row is null ? null : new AccountCodeAndType(row.Code, row.Type);
    }

    public Task<List<string>> ListCodesStartingWithAsync(string prefix, CancellationToken ct)
        => Query(new Specification<Account>(a => a.Code.StartsWith(prefix))).Select(a => a.Code).ToListAsync(ct);

    public Task<List<AccountReportRow>> ListReportRowsByCodeAsync(CancellationToken ct)
        => Query().OrderBy(a => a.Code).Select(a => new AccountReportRow(a.Id, a.Code, a.Name, a.Type, a.ReportingGroup)).ToListAsync(ct);

    public Task<List<Guid>> ListIncomeAndExpenseIdsAsync(CancellationToken ct)
        => Query(new Specification<Account>(a => a.Type == AccountType.Income || a.Type == AccountType.Expense)).Select(a => a.Id).ToListAsync(ct);

    public Task<List<Guid>> ListIncomeAndExpenseIdsByCodeAsync(CancellationToken ct)
        => Query(new Specification<Account>(a => a.Type == AccountType.Income || a.Type == AccountType.Expense)).OrderBy(a => a.Code).Select(a => a.Id).ToListAsync(ct);

    public Task<List<AccountHeader>> ListIncomeAndExpenseHeadersByCodeAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Account>(a => ids.Contains(a.Id) && (a.Type == AccountType.Income || a.Type == AccountType.Expense))).OrderBy(a => a.Code)
            .Select(a => new AccountHeader(a.Id, a.Code, a.Name, a.Type)).ToListAsync(ct);

    public Task<List<CashFlowAccount>> ListCashFlowChartAsync(CancellationToken ct)
        => Query().Select(a => new CashFlowAccount(a.Id, a.Code, a.Name, a.Type, a.ReportingGroup, a.SystemRole, a.IsBank)).ToListAsync(ct);

    public Task<List<AccountLabel>> ListActiveCashAccountsAsync(CancellationToken ct)
        => Query(new Specification<Account>(a => a.IsActive && a.Type == AccountType.Asset && (a.IsBank || a.SystemRole == "cash" || a.SystemRole == "bank"))).OrderBy(a => a.Code)
            .Select(a => new AccountLabel(a.Id, a.Code, a.Name)).ToListAsync(ct);

    // ---- the step-by-step chart of accounts ----

    // Four narrow columns per account: cheap even for a large chart, and portable across databases.
    public Task<List<AccountLink>> ListLinksAsync(CancellationToken ct)
        => Query().Select(a => new AccountLink(a.Id, a.ParentId, a.Type, a.IsActive)).ToListAsync(ct);

    public Task<List<AccountClassLink>> ListClassLinksAsync(CancellationToken ct)
        => Query().Select(a => new AccountClassLink(a.Id, a.ParentId, a.Type, a.IsActive, a.Code, a.ReportingGroup, a.SystemRole)).ToListAsync(ct);

    public Task<List<AccountClassRow>> ListPrimaryClassRowsAsync(AccountType type, CancellationToken ct)
    {
        var all = Query();
        return all.Where(a => a.Type == type && (a.ParentId == null || !all.Any(p => p.Id == a.ParentId && p.Type == type)))
            .Select(a => new AccountClassRow(a.Id, a.Code, a.ReportingGroup, a.SystemRole)).ToListAsync(ct);
    }

    public Task<PagedResult<AccountNodeDto>> PagePrimaryNodesAsync(AccountType type, PagedQuery query, CancellationToken ct)
    {
        var all = Query();
        var spec = new Specification<Account>(a => a.Type == type && (a.ParentId == null || !all.Any(p => p.Id == a.ParentId && p.Type == type)));
        return PageAsync(query, NodeProjection(all), spec, ct);
    }

    public Task<PagedResult<AccountNodeDto>> PageNodesAsync(List<Guid> ids, PagedQuery query, CancellationToken ct)
        => PageAsync(query, NodeProjection(Query()), new Specification<Account>(a => ids.Contains(a.Id)), ct);

    public Task<PagedResult<AccountNodeDto>> PageChildNodesAsync(Guid parentId, PagedQuery query, CancellationToken ct)
        => PageAsync(query, NodeProjection(Query()), new Specification<Account>(a => a.ParentId == parentId), ct);

    public Task<AccountNodeDto?> FindNodeAsync(Guid id, CancellationToken ct)
    {
        var all = Query();
        return all.Where(a => a.Id == id).Select(NodeProjection(all)).FirstOrDefaultAsync(ct);
    }

    public Task<List<AccountChildLink>> ListChildLinksAsync(List<Guid> parentIds, CancellationToken ct)
        => Query(new Specification<Account>(a => a.ParentId != null && parentIds.Contains(a.ParentId.Value)))
            .Select(a => new AccountChildLink(a.Id, a.ParentId!.Value, a.Type)).ToListAsync(ct);

    /// <summary>A tree node with its child count; balance and sub-category are filled in by the handler.</summary>
    private static Expression<Func<Account, AccountNodeDto>> NodeProjection(IQueryable<Account> all) => a => new AccountNodeDto(
        a.Id, a.Code, a.Name, a.Type, a.ParentId, a.ReportingGroup, a.SystemRole, a.IsPostable, a.IsBank, a.IsSystem, a.IsActive, a.CurrencyCode,
        all.Count(c => c.ParentId == a.Id), all.Any(c => c.ParentId == a.Id), null, null);
    public Task<List<AccountRole>> ListPostableRolesAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Account>(a => ids.Contains(a.Id) && a.IsPostable && a.IsActive)).Select(a => new AccountRole(a.Id, a.SystemRole)).ToListAsync(ct);

    public Task<List<CashAccount>> ListCashAndBankAsync(CancellationToken ct)
        => Query(new Specification<Account>(a => a.IsActive && a.Type == AccountType.Asset && (a.IsBank || a.SystemRole == "cash" || a.SystemRole == "bank"))).OrderBy(a => a.Code)
            .Select(a => new CashAccount(a.Id, a.Code, a.Name, a.IsBank)).ToListAsync(ct);

    public Task<Dictionary<Guid, AccountLabel>> GetLabelsAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Account>(a => ids.Contains(a.Id)))
            .Select(a => new AccountLabel(a.Id, a.Code, a.Name)).ToDictionaryAsync(a => a.Id, ct);

    public Task<List<AccountClassification>> ListClassificationsAsync(CancellationToken ct)
        => Query().Select(a => new AccountClassification(a.Id, a.Type, a.SystemRole, a.IsBank, a.ReportingGroup)).ToListAsync(ct);

    public Task<List<string>> ListCodesAsync(CancellationToken ct) => Query().Select(a => a.Code).ToListAsync(ct);

    public Task<List<string>> ListSystemRolesAsync(CancellationToken ct) => Query().Where(a => a.SystemRole != null).Select(a => a.SystemRole!).ToListAsync(ct);

    public Task<Guid?> FindActiveIdForRoleAsync(string systemRole, CancellationToken ct)
        => Query(new Specification<Account>(a => a.SystemRole == systemRole && a.IsActive)).Select(a => (Guid?)a.Id).FirstOrDefaultAsync(ct);
}
