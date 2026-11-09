using ERP.Application.Abstractions;
using ERP.Domain.Organizations;

namespace ERP.Application.Organizations;

/// <summary>Data access for business units. Implemented in ERP.Infrastructure/Repositories/Organizations/BusinessUnitRepository.cs.</summary>
public interface IBusinessUnitRepository : IRepository<BusinessUnit>
{
    /// <summary>The name of every business unit, by id.</summary>
    Task<Dictionary<Guid, string>> NamesAsync(CancellationToken ct);
}
