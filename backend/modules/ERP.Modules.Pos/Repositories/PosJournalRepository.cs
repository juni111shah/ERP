using ERP.Application.Abstractions;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Pos.Repositories;

internal sealed class PosJournalRepository(IRepository<JournalEntry> inner) : RepositoryBase<JournalEntry>(inner), IPosJournalRepository
{
    public Task<JournalEntry?> FindPostedForSourceForUpdateAsync(string sourceType, Guid sourceId, CancellationToken ct)
        => Tracked(new Specification<JournalEntry>(j => j.SourceType == sourceType && j.SourceId == sourceId && j.Status == JournalStatus.Posted).Include(j => j.Lines))
            .FirstOrDefaultAsync(ct);
}
