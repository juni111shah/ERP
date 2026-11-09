using ERP.Application.Abstractions;
using ERP.Domain.Identity;

namespace ERP.Application.Security;

/// <summary>Data access for the roles given to workspace members. Implemented in ERP.Infrastructure/Repositories/Security/UserRoleRepository.cs.</summary>
public interface IUserRoleRepository : IRepository<UserRole>
{
    /// <summary>The ids of the roles <paramref name="userId"/> holds.</summary>
    Task<List<Guid>> ListRoleIdsOfUserAsync(Guid userId, CancellationToken ct);

    /// <summary>The role assignments of <paramref name="userId"/>, tracked for changes.</summary>
    Task<List<UserRole>> ListForUserForUpdateAsync(Guid userId, CancellationToken ct);

    /// <summary>The users other than <paramref name="exceptUserId"/> who hold one of <paramref name="roleIds"/>.</summary>
    Task<List<Guid>> ListOtherHoldersAsync(List<Guid> roleIds, Guid exceptUserId, CancellationToken ct);
}
