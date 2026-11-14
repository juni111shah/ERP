using ERP.Application.Common;
using ERP.Domain.Identity;

namespace ERP.Application.Security;

internal sealed class UpdateRoleHandler(
    IRoleRepository roles, IRolePermissionRepository rolePermissions, IUserRoleRepository userRoles, PermissionGrants grants, IAccessChangeNotifier access) : ICommandHandler<UpdateRoleCommand, CreatedRoleDto>
{
    public async Task<CreatedRoleDto> Handle(UpdateRoleCommand command, CancellationToken ct)
    {
        // 1. A custom role, known permissions, and only grants the caller holds.
        var i = command.Input;
        var role = await roles.GetWithPermissionsForUpdateAsync(command.Id, ct);
        if (role.IsBuiltIn) throw new ConflictException("Built-in roles cannot be edited. Create a custom role instead.");
        grants.EnsureKnown(i.Permissions);
        // Only what the edit adds is a grant: a permission the role already has may stay, even if the caller does not hold it.
        PermissionGrants.EnsureCanGrant(await grants.CallerGrantsAsync(ct), i.Permissions.Except(role.Permissions.Select(p => p.Permission)));
        var name = i.Name.Trim();
        if (name != role.Name && await roles.AnyAsync(r => r.Name == name, ct)) throw new ConflictException("A role with that name exists.");

        // 2. Rename and apply the permission changes.
        role.Name = name; role.Description = i.Description;
        // Apply only the difference: removing and re-adding an unchanged permission would collide with its unique index.
        var wanted = i.Permissions.Distinct().ToHashSet();
        var taken = role.Permissions.Where(p => !wanted.Contains(p.Permission)).ToList();
        foreach (var removed in taken) { role.Permissions.Remove(removed); rolePermissions.Remove(removed); }
        // Explicit Add: a new child with a client-generated key attached only through the navigation would be tracked as existing (Modified).
        rolePermissions.AddRange(wanted.Where(w => role.Permissions.All(p => p.Permission != w)).Select(w => new RolePermission { RoleId = role.Id, Permission = w }));
        await roles.SaveChangesAsync(ct);

        // 3. Everyone holding the role works with its new permissions at once.
        await access.UsersChangedAsync(role.OrgId, await userRoles.ListHoldersAsync(role.Id, ct), AccessChangeReasons.RoleChanged, narrowed: taken.Count > 0, ct);
        return new(role.Id, role.Name);
    }
}
