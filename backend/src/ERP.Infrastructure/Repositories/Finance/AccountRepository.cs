using ERP.Application.Abstractions;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Finance;

internal sealed class AccountRepository(IRepository<Account> accounts) : RepositoryBase<Account>(accounts), IAccountRepository
{
    public Task<Dictionary<Guid, AccountLabel>> GetLabelsAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Account>(a => ids.Contains(a.Id)))
            .Select(a => new AccountLabel(a.Id, a.Code, a.Name)).ToDictionaryAsync(a => a.Id, ct);

    public Task<List<AccountClassification>> ListClassificationsAsync(CancellationToken ct)
        => Query().Select(a => new AccountClassification(a.Id, a.Type, a.SystemRole, a.IsBank, a.ReportingGroup)).ToListAsync(ct);
}
