using ERP.Application.Abstractions;
using ERP.Application.Organizations;
using ERP.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Organizations;

internal sealed class BusinessUnitRepository(IRepository<BusinessUnit> businessUnits) : RepositoryBase<BusinessUnit>(businessUnits), IBusinessUnitRepository
{
    public Task<Dictionary<Guid, string>> NamesAsync(CancellationToken ct)
        => Query().Select(b => new { b.Id, b.Name }).ToDictionaryAsync(b => b.Id, b => b.Name, ct);

    public Task<Dictionary<Guid, string>> GetNamesAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<BusinessUnit>(u => ids.Contains(u.Id))).ToDictionaryAsync(u => u.Id, u => u.Name, ct);
}
