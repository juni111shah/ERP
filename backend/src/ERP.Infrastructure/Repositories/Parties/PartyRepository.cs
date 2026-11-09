using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Parties;
using ERP.Domain.Parties;

namespace ERP.Infrastructure.Repositories.Parties;

internal sealed class PartyRepository(IRepository<Party> parties) : RepositoryBase<Party>(parties), IPartyRepository
{
    public Task<PagedResult<Party>> PageWithRoleAsync(PagedQuery query, PartyRoles? role, CancellationToken ct)
    {
        Specification<Party>? spec = null;
        if (role is { } r) spec = new Specification<Party>(p => (p.Roles & r) == r);
        return PageAsync(query, spec, ct);
    }
}
