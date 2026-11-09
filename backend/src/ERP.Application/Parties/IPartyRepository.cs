using ERP.Application.Abstractions;
using ERP.Domain.Parties;

namespace ERP.Application.Parties;

/// <summary>Data access for parties (customers and vendors). Implemented in ERP.Infrastructure/Repositories/Parties/PartyRepository.cs.</summary>
public interface IPartyRepository : IRepository<Party>
{
    /// <summary>The party group of each party (only the parties of <paramref name="partyGroupId"/> when it is set).</summary>
    Task<Dictionary<Guid, Guid?>> GroupOfAsync(Guid? partyGroupId, CancellationToken ct);
}
