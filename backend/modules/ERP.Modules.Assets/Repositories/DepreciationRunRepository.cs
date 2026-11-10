using ERP.Application.Abstractions;
using ERP.Modules.Assets.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Assets.Repositories;

internal sealed class DepreciationRunRepository(IRepository<DepreciationRun> inner) : RepositoryBase<DepreciationRun>(inner), IDepreciationRunRepository
{
    public Task<DepreciationRun?> FindForPeriodAsync(Guid legalEntityId, int year, int month, CancellationToken ct)
        => Query(new Specification<DepreciationRun>(r => r.LegalEntityId == legalEntityId && r.Year == year && r.Month == month)).FirstOrDefaultAsync(ct);

    public Task<DateOnly?> LatestPeriodEndAfterAsync(Guid legalEntityId, DateOnly periodEnd, CancellationToken ct)
        => Query(new Specification<DepreciationRun>(r => r.LegalEntityId == legalEntityId && r.PeriodEnd > periodEnd)).OrderByDescending(r => r.PeriodEnd).Select(r => (DateOnly?)r.PeriodEnd).FirstOrDefaultAsync(ct);

    public Task<Dictionary<Guid, Guid?>> MapJournalIdsAsync(List<Guid> runIds, CancellationToken ct)
        => Query(new Specification<DepreciationRun>(r => runIds.Contains(r.Id))).Select(r => new { r.Id, r.JournalEntryId }).ToDictionaryAsync(r => r.Id, r => r.JournalEntryId, ct);
}
