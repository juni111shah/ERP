using ERP.Application.Abstractions;
using ERP.Domain.Organizations;

namespace ERP.Application.Organizations;

/// <summary>Data access for workspaces (organizations). Implemented in ERP.Infrastructure/Repositories/Organizations/OrganizationRepository.cs.</summary>
public interface IOrganizationRepository : IRepository<Organization>
{
    /// <summary>The client workspaces (not the platform's own), or only <paramref name="onlyOrg"/>.</summary>
    Task<List<Organization>> ListClientsAsync(Guid? onlyOrg, CancellationToken ct);

    /// <summary>Every workspace's name by id.</summary>
    Task<Dictionary<Guid, string>> NamesByIdAsync(CancellationToken ct);
}
