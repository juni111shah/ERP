using ERP.Application.Abstractions;
using ERP.Application.Security;
using ERP.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Security;

internal sealed class RolePermissionRepository(IRepository<RolePermission> permissions) : RepositoryBase<RolePermission>(permissions), IRolePermissionRepository
{
    public Task<List<string>> ListPermissionsOfRolesAsync(IReadOnlyCollection<Guid> roleIds, CancellationToken ct)
        => Query(new Specification<RolePermission>(p => roleIds.Contains(p.RoleId))).Select(p => p.Permission).ToListAsync(ct);
}
