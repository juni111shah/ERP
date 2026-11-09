using ERP.Application.Abstractions;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Purchasing.Repositories;

internal sealed class PurchasingAccountRepository(IRepository<Account> inner) : RepositoryBase<Account>(inner), IPurchasingAccountRepository
{
    public Task<Account?> FindPostableByRoleAsync(string role, CancellationToken ct)
        => Query(new Specification<Account>(a => a.SystemRole == role && a.IsActive && a.IsPostable)).FirstOrDefaultAsync(ct);
}
