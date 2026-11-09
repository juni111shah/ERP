using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Parties;

namespace ERP.Application.Parties;

/// <summary>Data access for parties. Implemented in ERP.Infrastructure/Repositories/Parties/PartyRepository.cs.</summary>
public interface IPartyRepository : IRepository<Party>
{
    /// <summary>The generic paged list (search / filter / sort), limited to parties holding <paramref name="role"/> when one is given.</summary>
    Task<PagedResult<Party>> PageWithRoleAsync(PagedQuery query, PartyRoles? role, CancellationToken ct);
}
