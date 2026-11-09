using ERP.Application.Abstractions;
using ERP.Domain.Identity;

namespace ERP.Application.Security;

/// <summary>Data access for roles. Implemented in ERP.Infrastructure/Repositories/Security/RoleRepository.cs.</summary>
public interface IRoleRepository : IRepository<Role>
{
    /// <summary>The role with its permissions, tracked for changes. Throws NotFoundException.</summary>
    Task<Role> GetWithPermissionsForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>Which of <paramref name="ids"/> are roles of this workspace.</summary>
    Task<List<Guid>> ListExistingIdsAsync(Guid[] ids, CancellationToken ct);

    /// <summary>The roles that grant everything ("*").</summary>
    Task<List<Guid>> ListAdministratorRoleIdsAsync(CancellationToken ct);

    /// <summary>The names of every role of this workspace.</summary>
    Task<List<string>> ListNamesAsync(CancellationToken ct);
}
