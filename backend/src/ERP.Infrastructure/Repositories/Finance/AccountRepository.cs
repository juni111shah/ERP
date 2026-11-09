using ERP.Application.Abstractions;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Finance;

internal sealed class AccountRepository(IRepository<Account> accounts) : RepositoryBase<Account>(accounts), IAccountRepository
{
    public Task<List<AccountRole>> ListPostableRolesAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Account>(a => ids.Contains(a.Id) && a.IsPostable && a.IsActive)).Select(a => new AccountRole(a.Id, a.SystemRole)).ToListAsync(ct);

    public Task<List<CashAccount>> ListCashAndBankAsync(CancellationToken ct)
        => Query(new Specification<Account>(a => a.IsActive && a.Type == AccountType.Asset && (a.IsBank || a.SystemRole == "cash" || a.SystemRole == "bank"))).OrderBy(a => a.Code)
            .Select(a => new CashAccount(a.Id, a.Code, a.Name, a.IsBank)).ToListAsync(ct);
}
