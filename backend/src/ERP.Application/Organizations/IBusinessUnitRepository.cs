using ERP.Application.Abstractions;
using ERP.Domain.Organizations;

namespace ERP.Application.Organizations;

/// <summary>Data access for business units (branches). Implemented in ERP.Infrastructure/Repositories/Organizations/BusinessUnitRepository.cs.</summary>
public interface IBusinessUnitRepository : IRepository<BusinessUnit>
{
    /// <summary>Business unit names by id, for the ids that exist.</summary>
    Task<Dictionary<Guid, string>> GetNamesAsync(List<Guid> ids, CancellationToken ct);
}
