using ERP.Application.Abstractions;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Features;

namespace ERP.Modules.RealEstate.Repositories;

/// <summary>Data access for the asking rent history of units. Implemented in UnitRentRateRepository.cs.</summary>
public interface IUnitRentRateRepository : IRepository<UnitRentRate>
{
    /// <summary>The rent history of a unit, latest first.</summary>
    Task<List<UnitRentRateDto>> ListHistoryAsync(Guid unitId, CancellationToken ct);

    /// <summary>The rate a unit got on a given day, tracked for changes; null when there is none.</summary>
    Task<UnitRentRate?> FindForDayForUpdateAsync(Guid unitId, DateOnly day, CancellationToken ct);
}
