using ERP.Application.Abstractions;
using ERP.Domain.Parties;

namespace ERP.Application.Parties;

/// <summary>Data access for party groups. Implemented in ERP.Infrastructure/Repositories/Parties/PartyGroupRepository.cs.</summary>
public interface IPartyGroupRepository : IRepository<PartyGroup>
{
    /// <summary>The name of every party group, by id.</summary>
    Task<Dictionary<Guid, string>> NamesAsync(CancellationToken ct);
}
