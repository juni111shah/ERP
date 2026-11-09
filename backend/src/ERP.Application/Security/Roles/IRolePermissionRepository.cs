using ERP.Application.Abstractions;
using ERP.Domain.Identity;

namespace ERP.Application.Security;

/// <summary>Data access for the permissions held by roles. Implemented in ERP.Infrastructure/Repositories/Security/RolePermissionRepository.cs.</summary>
public interface IRolePermissionRepository : IRepository<RolePermission>
{
    /// <summary>The permissions the roles in <paramref name="roleIds"/> grant (with repeats).</summary>
    Task<List<string>> ListPermissionsOfRolesAsync(IReadOnlyCollection<Guid> roleIds, CancellationToken ct);
}
