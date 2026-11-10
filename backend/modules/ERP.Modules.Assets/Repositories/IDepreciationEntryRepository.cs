using ERP.Application.Abstractions;
using ERP.Modules.Assets.Domain;

namespace ERP.Modules.Assets.Repositories;

/// <summary>Data access for the depreciation charged per asset and month.</summary>
public interface IDepreciationEntryRepository : IRepository<DepreciationEntry>
{
    /// <summary>The last month-end an asset was charged for, or null when it never was.</summary>
    Task<DateOnly?> LastPeriodEndForAssetAsync(Guid assetId, CancellationToken ct);

    /// <summary>The depreciation charged up to <paramref name="asOf"/> on each of the assets that has any, by asset id.</summary>
    Task<Dictionary<Guid, decimal>> ChargedByAssetAsync(List<Guid> assetIds, DateOnly asOf, CancellationToken ct);

    /// <summary>Every charge of one asset.</summary>
    Task<List<DepreciationEntry>> ListForAssetAsync(Guid assetId, CancellationToken ct);

    /// <summary>Every charge of one run.</summary>
    Task<List<DepreciationEntry>> ListForRunAsync(Guid runId, CancellationToken ct);

    /// <summary>Month-end and amount of each charge for a month-end from <paramref name="from"/> to <paramref name="to"/>, of one legal entity or (null) all of them.</summary>
    Task<List<DepreciationAmount>> ListAmountsAsync(DateOnly from, DateOnly to, Guid? legalEntityId, CancellationToken ct);
}

/// <summary>The amount of one charge and the month-end it is for.</summary>
public sealed record DepreciationAmount(DateOnly PeriodEnd, decimal Amount);
