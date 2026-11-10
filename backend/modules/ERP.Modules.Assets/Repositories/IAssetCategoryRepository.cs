using ERP.Application.Abstractions;
using ERP.Modules.Assets.Domain;

namespace ERP.Modules.Assets.Repositories;

/// <summary>Data access for asset categories.</summary>
public interface IAssetCategoryRepository : IRepository<AssetCategory>
{
    /// <summary>The name of every category, by id.</summary>
    Task<Dictionary<Guid, string>> MapNamesAsync(CancellationToken ct);

    /// <summary>The categories with the given ids, by id.</summary>
    Task<Dictionary<Guid, AssetCategory>> MapByIdsAsync(List<Guid> ids, CancellationToken ct);
}
