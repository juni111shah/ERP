using ERP.Application.Abstractions;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Repositories;

/// <summary>Data access for unit types. Implemented in UnitTypeRepository.cs.</summary>
public interface IUnitTypeRepository : IRepository<UnitType>
{
    /// <summary>The name of each of the unit types, by id.</summary>
    Task<Dictionary<Guid, string>> NamesAsync(List<Guid> ids, CancellationToken ct);
}
