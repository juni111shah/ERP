using ERP.Application.Abstractions;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>Data access for fiscal year closes. Implemented in ERP.Infrastructure/Repositories/Finance/FiscalYearCloseRepository.cs.</summary>
public interface IFiscalYearCloseRepository : IRepository<FiscalYearClose>
{
    /// <summary>The end of the latest fiscal year of the entity that is closed and not reopened; null when none is.</summary>
    Task<DateOnly?> GetLatestClosedYearEndAsync(Guid legalEntityId, CancellationToken ct);

    /// <summary>The legal entities with a closed (not reopened) fiscal year ending on or before <paramref name="asOf"/>.</summary>
    Task<List<Guid>> ListEntitiesClosedByAsync(DateOnly asOf, Guid? entityId, CancellationToken ct);

    /// <summary>The close of the fiscal year that is not reopened, read-only; null when the year is not closed.</summary>
    Task<FiscalYearClose?> FindLiveAsync(Guid legalEntityId, int fiscalYear, CancellationToken ct);

    /// <summary>The close of the fiscal year that is not reopened, tracked for changes; null when the year is not closed.</summary>
    Task<FiscalYearClose?> FindLiveForUpdateAsync(Guid legalEntityId, int fiscalYear, CancellationToken ct);
}
