using ERP.Application.Abstractions;
using ERP.Modules.Assets.Domain;

namespace ERP.Modules.Assets.Repositories;

/// <summary>Data access for the asset register.</summary>
public interface IFixedAssetRepository : IRepository<FixedAsset>
{
    /// <summary>The cost of the other capitalised assets taken from one bill line.</summary>
    Task<decimal> CostCapitalisedFromBillLineAsync(Guid billLineId, Guid excludeAssetId, CancellationToken ct);

    /// <summary>The assets on the books at the end of <paramref name="asOf"/> (capitalised by then, not yet disposed), by number.</summary>
    Task<List<FixedAsset>> ListOnBooksAsync(DateOnly asOf, Guid? legalEntityId, Guid? categoryId, CancellationToken ct);

    /// <summary>The capitalised (still depreciating) assets of one legal entity or (null) all of them.</summary>
    Task<List<FixedAsset>> ListCapitalisedAsync(Guid? legalEntityId, CancellationToken ct);

    /// <summary>The capitalised assets of a legal entity capitalised on or before <paramref name="periodEnd"/>, tracked for changes.</summary>
    Task<List<FixedAsset>> ListDueForDepreciationForUpdateAsync(Guid legalEntityId, DateOnly periodEnd, CancellationToken ct);

    /// <summary>Number and name of each of the assets that exists, by id.</summary>
    Task<Dictionary<Guid, AssetNumberAndName>> MapNumbersAndNamesAsync(List<Guid> ids, CancellationToken ct);
}

/// <summary>Number and name of an asset.</summary>
public sealed record AssetNumberAndName(Guid Id, string Number, string Name);
