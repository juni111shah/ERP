using ERP.Application.Common;
using ERP.Domain.Identity;

namespace ERP.Application.Security;

internal sealed class CreateRoleHandler(IRoleRepository roles, PermissionGrants grants) : ICommandHandler<CreateRoleCommand, CreatedRoleDto>
{
    public async Task<CreatedRoleDto> Handle(CreateRoleCommand command, CancellationToken ct)
    {
        // 1. Known permissions, all held by the caller, and a free name.
        var i = command.Input;
        grants.EnsureKnown(i.Permissions);
        PermissionGrants.EnsureCanGrant(await grants.CallerGrantsAsync(ct), i.Permissions);
        var name = i.Name.Trim();
        if (await roles.AnyAsync(r => r.Name == name, ct)) throw new ConflictException("A role with that name exists.");

        // 2. Create the role.
        var role = new Role { Name = name, Description = i.Description };
        role.Permissions.AddRange(i.Permissions.Distinct().Select(p => new RolePermission { Permission = p }));
        roles.Add(role);
        await roles.SaveChangesAsync(ct);
        return new(role.Id, role.Name);
    }
}
