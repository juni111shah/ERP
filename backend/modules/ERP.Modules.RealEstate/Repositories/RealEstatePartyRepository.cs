using ERP.Application.Abstractions;
using ERP.Domain.Parties;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.RealEstate.Repositories;

internal sealed class RealEstatePartyRepository(IRepository<Party> inner) : RepositoryBase<Party>(inner), IRealEstatePartyRepository
{
    public Task<Dictionary<Guid, string>> NamesAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Party>(p => ids.Contains(p.Id))).Select(p => new { p.Id, p.Name }).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
}
