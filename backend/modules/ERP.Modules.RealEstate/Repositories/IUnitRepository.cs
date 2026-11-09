using ERP.Application.Abstractions;
using ERP.Modules.RealEstate.Domain;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Repositories;

/// <summary>Data access for units. Implemented in UnitRepository.cs.</summary>
public interface IUnitRepository : IRepository<Unit>
{
    /// <summary>Those of the unit numbers that already exist in the property.</summary>
    Task<List<string>> ListExistingNumbersAsync(Guid propertyId, List<string> numbers, CancellationToken ct);

    /// <summary>The number of each of the units, by id.</summary>
    Task<Dictionary<Guid, string>> NumbersAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>Reserved units whose reservation ended before <paramref name="today"/>, tracked for changes.</summary>
    Task<List<Unit>> ListLapsedReservationsForUpdateAsync(DateOnly today, CancellationToken ct);

    /// <summary>The number and asking rent of each of the units, by id.</summary>
    Task<Dictionary<Guid, UnitNumberAndRent>> MapNumberAndAskingRentAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>What the rent roll shows of each of the units, by id.</summary>
    Task<Dictionary<Guid, RentRollUnit>> MapRentRollUnitsAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>Status and asking rent of the units of one legal entity or (null) all.</summary>
    Task<List<UnitOccupancyRow>> ListOccupancyRowsAsync(Guid? legalEntityId, CancellationToken ct);

    /// <summary>The status of the units of one legal entity or (null) all.</summary>
    Task<List<UnitStatusRow>> ListStatusesAsync(Guid? legalEntityId, CancellationToken ct);
}

/// <summary>A unit's number and asking rent.</summary>
public sealed record UnitNumberAndRent(Guid Id, string UnitNumber, decimal AskingRent);

/// <summary>A unit as the rent roll shows it.</summary>
public sealed record RentRollUnit(Guid Id, string UnitNumber, decimal AreaSqft, Guid UnitTypeId, UnitUsage Usage);

/// <summary>A unit's property, status and asking rent.</summary>
public sealed record UnitOccupancyRow(Guid Id, Guid PropertyId, UnitStatus Status, decimal AskingRent);

/// <summary>A unit's property and status.</summary>
public sealed record UnitStatusRow(Guid PropertyId, UnitStatus Status);
