using ERP.Application.Abstractions;
using ERP.Modules.Assets.Domain;

namespace ERP.Modules.Assets.Repositories;

/// <summary>Data access for monthly depreciation runs.</summary>
public interface IDepreciationRunRepository : IRepository<DepreciationRun>
{
    /// <summary>The run of a legal entity for one month, or null when that month has not run.</summary>
    Task<DepreciationRun?> FindForPeriodAsync(Guid legalEntityId, int year, int month, CancellationToken ct);

    /// <summary>The latest month-end after <paramref name="periodEnd"/> that a legal entity has already run, or null.</summary>
    Task<DateOnly?> LatestPeriodEndAfterAsync(Guid legalEntityId, DateOnly periodEnd, CancellationToken ct);

    /// <summary>The journal of each of the runs that exists, by run id.</summary>
    Task<Dictionary<Guid, Guid?>> MapJournalIdsAsync(List<Guid> runIds, CancellationToken ct);
}
