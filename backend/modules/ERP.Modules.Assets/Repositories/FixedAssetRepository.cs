using ERP.Application.Abstractions;
using ERP.Modules.Assets.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Assets.Repositories;

internal sealed class FixedAssetRepository(IRepository<FixedAsset> inner) : RepositoryBase<FixedAsset>(inner), IFixedAssetRepository
{
    public async Task<decimal> CostCapitalisedFromBillLineAsync(Guid billLineId, Guid excludeAssetId, CancellationToken ct)
        => (await Query(new Specification<FixedAsset>(a => a.SourceBillLineId == billLineId && a.Id != excludeAssetId && a.Status != AssetStatus.Draft)).Select(a => a.Cost).ToListAsync(ct)).Sum();

    public Task<List<FixedAsset>> ListOnBooksAsync(DateOnly asOf, Guid? legalEntityId, Guid? categoryId, CancellationToken ct)
        => Query(new Specification<FixedAsset>(a => a.CapitalisedOn != null && a.CapitalisedOn <= asOf && (a.DisposedOn == null || a.DisposedOn > asOf)
                && (legalEntityId == null || a.LegalEntityId == legalEntityId) && (categoryId == null || a.CategoryId == categoryId)))
            .OrderBy(a => a.Number).ToListAsync(ct);

    public Task<List<FixedAsset>> ListCapitalisedAsync(Guid? legalEntityId, CancellationToken ct)
        => Query(new Specification<FixedAsset>(a => (a.Status == AssetStatus.Capitalised) && (legalEntityId == null || a.LegalEntityId == legalEntityId))).ToListAsync(ct);

    public Task<List<FixedAsset>> ListDueForDepreciationForUpdateAsync(Guid legalEntityId, DateOnly periodEnd, CancellationToken ct)
        => Tracked(new Specification<FixedAsset>(a => a.LegalEntityId == legalEntityId && a.Status == AssetStatus.Capitalised && a.CapitalisedOn <= periodEnd)).ToListAsync(ct);

    public Task<Dictionary<Guid, AssetNumberAndName>> MapNumbersAndNamesAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<FixedAsset>(a => ids.Contains(a.Id))).Select(a => new AssetNumberAndName(a.Id, a.Number, a.Name)).ToDictionaryAsync(a => a.Id, ct);
}
