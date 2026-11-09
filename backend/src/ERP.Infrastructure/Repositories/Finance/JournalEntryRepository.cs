using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Finance;

internal sealed class JournalEntryRepository(IRepository<JournalEntry> journals) : RepositoryBase<JournalEntry>(journals), IJournalEntryRepository
{
    public async Task<JournalEntry> GetWithLinesForUpdateAsync(Guid? id, CancellationToken ct)
        => await Tracked(new Specification<JournalEntry>(j => j.Id == id).Include(j => j.Lines)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    public Task<List<AccountLedgerLine>> ListAccountLinesAsync(Guid accountId, Guid? entityId, DateOnly asOf, CancellationToken ct)
        => Query(new Specification<JournalEntry>(j => j.Status != JournalStatus.Draft && j.Date <= asOf && (entityId == null || j.LegalEntityId == entityId)))
            .SelectMany(j => j.Lines.Where(l => l.AccountId == accountId), (j, l) => new AccountLedgerLine(l.Id, j.Id, j.Number, j.Date, l.Description ?? j.Memo, l.Debit, l.Credit)).ToListAsync(ct);

    public Task<List<AccountMovement>> ListMovementsAsync(List<Guid> accountIds, DateOnly asOf, Guid? entityId, CancellationToken ct)
        => Query(new Specification<JournalEntry>(j => j.Status != JournalStatus.Draft && j.Date <= asOf && (entityId == null || j.LegalEntityId == entityId)))
            .SelectMany(j => j.Lines.Where(l => accountIds.Contains(l.AccountId)), (j, l) => new AccountMovement(l.AccountId, l.Debit, l.Credit)).ToListAsync(ct);
}
