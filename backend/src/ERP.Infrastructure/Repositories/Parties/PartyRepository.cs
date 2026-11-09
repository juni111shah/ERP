using ERP.Application.Abstractions;
using ERP.Application.Parties;
using ERP.Domain.Parties;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Parties;

internal sealed class PartyRepository(IRepository<Party> parties) : RepositoryBase<Party>(parties), IPartyRepository
{
    public Task<Dictionary<Guid, Guid?>> GroupOfAsync(Guid? partyGroupId, CancellationToken ct)
        => Query(new Specification<Party>(p => partyGroupId == null || p.PartyGroupId == partyGroupId)).Select(p => new { p.Id, p.PartyGroupId }).ToDictionaryAsync(p => p.Id, p => p.PartyGroupId, ct);
}
