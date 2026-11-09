using ERP.Application.Abstractions;
using ERP.Modules.Assets.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Assets.Repositories;

internal sealed class AssetCategoryRepository(IRepository<AssetCategory> inner) : RepositoryBase<AssetCategory>(inner), IAssetCategoryRepository
{
    public Task<Dictionary<Guid, string>> MapNamesAsync(CancellationToken ct)
        => Query().Select(c => new { c.Id, c.Name }).ToDictionaryAsync(c => c.Id, c => c.Name, ct);

    public Task<Dictionary<Guid, AssetCategory>> MapByIdsAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<AssetCategory>(c => ids.Contains(c.Id))).ToDictionaryAsync(c => c.Id, ct);
}
