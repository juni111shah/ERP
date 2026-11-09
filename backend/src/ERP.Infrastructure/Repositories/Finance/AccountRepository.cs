using ERP.Application.Abstractions;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Finance;

internal sealed class AccountRepository(IRepository<Account> accounts) : RepositoryBase<Account>(accounts), IAccountRepository
{
    public Task<List<string>> ListCodesAsync(CancellationToken ct) => Query().Select(a => a.Code).ToListAsync(ct);

    public Task<List<string>> ListSystemRolesAsync(CancellationToken ct) => Query().Where(a => a.SystemRole != null).Select(a => a.SystemRole!).ToListAsync(ct);
}
