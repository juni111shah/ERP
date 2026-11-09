using ERP.Application.Abstractions;
using ERP.Application.Organizations;
using ERP.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Organizations;

internal sealed class BusinessUnitRepository(IRepository<BusinessUnit> units) : RepositoryBase<BusinessUnit>(units), IBusinessUnitRepository
{
    public Task<Dictionary<Guid, string>> GetNamesAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<BusinessUnit>(u => ids.Contains(u.Id))).ToDictionaryAsync(u => u.Id, u => u.Name, ct);
}
