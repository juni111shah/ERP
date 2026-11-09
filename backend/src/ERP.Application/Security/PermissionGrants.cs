using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Application.Security;

/// <summary>
/// The rules about handing out permissions, shared by the role and user commands: nobody grants more than they hold, and the
/// workspace always keeps one active administrator.
/// </summary>
internal sealed class PermissionGrants(
    IPermissionCatalog catalog, IRoleRepository roles, IUserRoleRepository userRoles, IRolePermissionRepository rolePermissions, IOrganizationMemberRepository members, ICurrentUser currentUser)
{
    /// <summary>Every permission must exist (or be "*").</summary>
    public void EnsureKnown(IEnumerable<string> permissions)
    {
        var unknown = permissions.Where(p => p != CorePermissions.All && catalog.All.All(d => d.Key != p)).ToList();
        if (unknown.Count > 0) throw new FieldValidationException("permissions", $"Unknown permissions: {string.Join(", ", unknown)}");
    }

    /// <summary>What the caller's roles grant right now, read from the database: the token expands "*" and can be minutes old.</summary>
    public async Task<HashSet<string>> CallerGrantsAsync(CancellationToken ct)
    {
        var me = currentUser.UserId ?? throw new ForbiddenException();
        var roleIds = await userRoles.ListRoleIdsOfUserAsync(me, ct);
        return (await rolePermissions.ListPermissionsOfRolesAsync(roleIds, ct)).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Nobody hands out more than they hold, and only a "*" holder hands out "*".</summary>
    public static void EnsureCanGrant(HashSet<string> held, IEnumerable<string> wanted)
    {
        if (held.Contains(CorePermissions.All)) return;
        var beyond = wanted.Where(p => !held.Contains(p)).Distinct().Order(StringComparer.Ordinal).ToList();
        if (beyond.Count > 0) throw new ForbiddenException($"You can only grant permissions you hold yourself. Not held: {string.Join(", ", beyond)}");
    }

    public async Task EnsureCanAssignAsync(HashSet<string> held, IReadOnlyCollection<Guid> roleIds, CancellationToken ct)
    {
        if (roleIds.Count == 0 || held.Contains(CorePermissions.All)) return;
        EnsureCanGrant(held, await rolePermissions.ListPermissionsOfRolesAsync(roleIds, ct));
    }

    /// <summary>Roles that grant everything ("*"): the workspace must always keep one active holder.</summary>
    public Task<List<Guid>> AdminRoleIdsAsync(CancellationToken ct) => roles.ListAdministratorRoleIdsAsync(ct);

    public async Task EnsureAnotherAdministratorAsync(Guid userId, List<Guid> adminRoleIds, CancellationToken ct)
    {
        var others = await userRoles.ListOtherHoldersAsync(adminRoleIds, userId, ct);
        if (!await members.AnyAsync(m => others.Contains(m.UserId) && m.IsActive, ct))
            throw new ConflictException("The workspace needs at least one active administrator.");
    }
}
