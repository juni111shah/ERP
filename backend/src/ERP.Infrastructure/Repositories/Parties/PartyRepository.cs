using ERP.Application.Abstractions;
using ERP.Application.Parties;
using ERP.Domain.Parties;
using Microsoft.EntityFrameworkCore;
using ERP.Application.Common;

namespace ERP.Infrastructure.Repositories.Parties;

internal sealed class PartyRepository(IRepository<Party> parties) : RepositoryBase<Party>(parties), IPartyRepository
{
    public Task<Dictionary<Guid, Guid?>> GroupOfAsync(Guid? partyGroupId, CancellationToken ct)
        => Query(new Specification<Party>(p => partyGroupId == null || p.PartyGroupId == partyGroupId)).Select(p => new { p.Id, p.PartyGroupId }).ToDictionaryAsync(p => p.Id, p => p.PartyGroupId, ct);

    public Task<PagedResult<Party>> PageWithRoleAsync(PagedQuery query, PartyRoles? role, CancellationToken ct)
    {
        Specification<Party>? spec = null;
        if (role is { } r) spec = new Specification<Party>(p => (p.Roles & r) == r);
        return PageAsync(query, spec, ct);
    }
}
