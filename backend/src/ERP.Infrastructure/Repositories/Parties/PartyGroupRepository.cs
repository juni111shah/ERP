using ERP.Application.Abstractions;
using ERP.Application.Parties;
using ERP.Domain.Parties;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Parties;

internal sealed class PartyGroupRepository(IRepository<PartyGroup> partyGroups) : RepositoryBase<PartyGroup>(partyGroups), IPartyGroupRepository
{
    public Task<Dictionary<Guid, string>> NamesAsync(CancellationToken ct)
        => Query().Select(g => new { g.Id, g.Name }).ToDictionaryAsync(g => g.Id, g => g.Name, ct);
}
