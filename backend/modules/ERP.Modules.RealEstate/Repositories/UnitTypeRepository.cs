using ERP.Application.Abstractions;
using ERP.Modules.RealEstate.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.RealEstate.Repositories;

internal sealed class UnitTypeRepository(IRepository<UnitType> inner) : RepositoryBase<UnitType>(inner), IUnitTypeRepository
{
    public Task<Dictionary<Guid, string>> NamesAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<UnitType>(t => ids.Contains(t.Id))).Select(t => new { t.Id, t.Name }).ToDictionaryAsync(t => t.Id, t => t.Name, ct);
}
