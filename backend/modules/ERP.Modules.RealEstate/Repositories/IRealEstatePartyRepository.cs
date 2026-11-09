using ERP.Application.Abstractions;
using ERP.Domain.Parties;

namespace ERP.Modules.RealEstate.Repositories;

/// <summary>What the real estate screens read from parties (tenants, owners, agents). Implemented in RealEstatePartyRepository.cs.</summary>
public interface IRealEstatePartyRepository : IRepository<Party>
{
    /// <summary>The name of each of the parties, by id.</summary>
    Task<Dictionary<Guid, string>> NamesAsync(List<Guid> ids, CancellationToken ct);
}
