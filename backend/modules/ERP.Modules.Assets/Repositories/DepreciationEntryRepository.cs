using ERP.Application.Abstractions;
using ERP.Modules.Assets.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Assets.Repositories;

internal sealed class DepreciationEntryRepository(IRepository<DepreciationEntry> inner) : RepositoryBase<DepreciationEntry>(inner), IDepreciationEntryRepository
{
    public Task<DateOnly?> LastPeriodEndForAssetAsync(Guid assetId, CancellationToken ct)
        => Query(new Specification<DepreciationEntry>(e => e.AssetId == assetId)).OrderByDescending(e => e.PeriodEnd).Select(e => (DateOnly?)e.PeriodEnd).FirstOrDefaultAsync(ct);

    public async Task<Dictionary<Guid, decimal>> ChargedByAssetAsync(List<Guid> assetIds, DateOnly asOf, CancellationToken ct)
        => (await Query(new Specification<DepreciationEntry>(e => assetIds.Contains(e.AssetId) && e.PeriodEnd <= asOf)).Select(e => new { e.AssetId, e.Amount }).ToListAsync(ct))
            .GroupBy(e => e.AssetId).ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

    public Task<List<DepreciationEntry>> ListForAssetAsync(Guid assetId, CancellationToken ct)
        => Query(new Specification<DepreciationEntry>(e => e.AssetId == assetId)).ToListAsync(ct);

    public Task<List<DepreciationEntry>> ListForRunAsync(Guid runId, CancellationToken ct)
        => Query(new Specification<DepreciationEntry>(e => e.RunId == runId)).ToListAsync(ct);

    public Task<List<DepreciationAmount>> ListAmountsAsync(DateOnly from, DateOnly to, Guid? legalEntityId, CancellationToken ct)
        => Query(new Specification<DepreciationEntry>(e => e.PeriodEnd >= from && e.PeriodEnd <= to && (legalEntityId == null || e.LegalEntityId == legalEntityId))).Select(e => new DepreciationAmount(e.PeriodEnd, e.Amount)).ToListAsync(ct);
}
