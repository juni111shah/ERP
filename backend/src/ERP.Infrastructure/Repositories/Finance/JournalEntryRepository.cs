using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Finance;

internal sealed class JournalEntryRepository(IRepository<JournalEntry> journals) : RepositoryBase<JournalEntry>(journals), IJournalEntryRepository
{
    public async Task<JournalEntry> GetWithLinesAsync(Guid id, CancellationToken ct)
        => await Query(new Specification<JournalEntry>(j => j.Id == id).Include(j => j.Lines)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    public Task<List<PostedJournalLine>> ListPostedLinesUpToAsync(DateOnly to, Guid? legalEntityId, CancellationToken ct)
        => Query(new Specification<JournalEntry>(j => j.Status != JournalStatus.Draft && j.Date <= to && (legalEntityId == null || j.LegalEntityId == legalEntityId)))
            .SelectMany(j => j.Lines, (j, l) => new PostedJournalLine(l.AccountId, j.Date, l.Debit, l.Credit, l.BusinessUnitId)).ToListAsync(ct);
}
