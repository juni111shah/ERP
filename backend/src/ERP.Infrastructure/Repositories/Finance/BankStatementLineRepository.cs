using ERP.Application.Abstractions;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Finance;

internal sealed class BankStatementLineRepository(IRepository<BankStatementLine> lines)
    : RepositoryBase<BankStatementLine>(lines), IBankStatementLineRepository
{
    public Task<List<StatementLineKey>> ListKeysAsync(Guid accountId, Guid entityId, CancellationToken ct)
        => Query(new Specification<BankStatementLine>(l => l.AccountId == accountId && l.LegalEntityId == entityId))
            .Select(l => new StatementLineKey(l.Date, l.Amount, l.Reference, l.Description)).ToListAsync(ct);

    public Task<List<BankStatementLine>> ListUnmatchedForUpdateAsync(Guid accountId, Guid entityId, CancellationToken ct)
        => Tracked(new Specification<BankStatementLine>(l => l.AccountId == accountId && l.LegalEntityId == entityId && l.Status == StatementLineStatus.Unmatched)).OrderBy(l => l.Date).ToListAsync(ct);

    public Task<List<BankStatementLine>> ListUnmatchedAsOfAsync(Guid accountId, DateOnly asOf, Guid? entityId, CancellationToken ct)
        => Query(new Specification<BankStatementLine>(l => l.AccountId == accountId && l.Date <= asOf && l.Status == StatementLineStatus.Unmatched && (entityId == null || l.LegalEntityId == entityId)))
            .OrderBy(l => l.Date).ToListAsync(ct);

    public async Task<HashSet<Guid>> MatchedJournalLineIdsAsync(Guid accountId, CancellationToken ct)
        => (await Query(new Specification<BankStatementLine>(l => l.AccountId == accountId && l.MatchedJournalLineId != null)).Select(l => l.MatchedJournalLineId!.Value).ToListAsync(ct)).ToHashSet();
}
