using ERP.Application.Abstractions;
using ERP.Domain.Organizations;

namespace ERP.Modularity.Features;

/// <summary>Data access for a workspace's module switches. Implemented in ERP.Infrastructure/Repositories/Platform/OrganizationModuleRepository.cs.</summary>
public interface IOrganizationModuleRepository : IRepository<OrganizationModule>
{
    /// <summary>The workspace's row for <paramref name="moduleKey"/>, tracked for changes, or null when it was never switched.</summary>
    Task<OrganizationModule?> FindForUpdateAsync(string moduleKey, CancellationToken ct);
}
