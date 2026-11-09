using ERP.Application.Abstractions;
using ERP.Application.Security;
using ERP.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Security;

internal sealed class UserRoleRepository(IRepository<UserRole> userRoles) : RepositoryBase<UserRole>(userRoles), IUserRoleRepository
{
    public Task<List<Guid>> ListRoleIdsOfUserAsync(Guid userId, CancellationToken ct)
        => Query(new Specification<UserRole>(ur => ur.UserId == userId)).Select(ur => ur.RoleId).ToListAsync(ct);

    public Task<List<UserRole>> ListForUserForUpdateAsync(Guid userId, CancellationToken ct)
        => Tracked(new Specification<UserRole>(ur => ur.UserId == userId)).ToListAsync(ct);

    public Task<List<Guid>> ListOtherHoldersAsync(List<Guid> roleIds, Guid exceptUserId, CancellationToken ct)
        => Query(new Specification<UserRole>(ur => roleIds.Contains(ur.RoleId) && ur.UserId != exceptUserId)).Select(ur => ur.UserId).Distinct().ToListAsync(ct);
}
