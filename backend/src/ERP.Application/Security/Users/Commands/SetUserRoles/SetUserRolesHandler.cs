using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Identity;

namespace ERP.Application.Security;

internal sealed class SetUserRolesHandler(
    IOrganizationMemberRepository members, IRoleRepository roles, IUserRoleRepository userRoles, IUserDirectory directory, PermissionGrants grants, IUnitOfWork unitOfWork, ICurrentUser currentUser,
    IAccessChangeNotifier access)
    : ICommandHandler<SetUserRolesCommand, UserSummary>
{
    public async Task<UserSummary> Handle(SetUserRolesCommand command, CancellationToken ct)
    {
        // 1. The member, the roles asked for and what changes.
        var member = await members.FindByUserForUpdateAsync(command.UserId, ct) ?? throw new NotFoundException("That user is not a member of this workspace.");
        var roleIds = await roles.ListExistingIdsAsync(command.RoleIds, ct);
        var current = await userRoles.ListForUserForUpdateAsync(command.UserId, ct);
        var added = roleIds.Where(id => current.All(ur => ur.RoleId != id)).ToList();

        // 2. Only an administrator changes their own roles; nobody hands out more than they hold; one administrator stays.
        var held = await grants.CallerGrantsAsync(ct);
        if (command.UserId == currentUser.UserId && !held.Contains(CorePermissions.All) && (added.Count > 0 || current.Any(ur => !roleIds.Contains(ur.RoleId))))
            throw new ForbiddenException("Only an administrator can change their own roles.");
        await grants.EnsureCanAssignAsync(held, added, ct);
        var adminRoleIds = await grants.AdminRoleIdsAsync(ct);
        var losesAdmin = current.Any(ur => adminRoleIds.Contains(ur.RoleId)) && !roleIds.Any(adminRoleIds.Contains);
        if (losesAdmin) await grants.EnsureAnotherAdministratorAsync(command.UserId, adminRoleIds, ct);

        // 3. Apply the difference.
        var taken = current.Where(ur => !roleIds.Contains(ur.RoleId)).ToList();
        foreach (var ur in taken) userRoles.Remove(ur);
        userRoles.AddRange(added.Select(id => new UserRole { UserId = command.UserId, RoleId = id }));
        await unitOfWork.SaveChangesAsync(ct);
        // 4. The member works with the new roles at once, without signing in again.
        await access.UsersChangedAsync(member.OrgId, [command.UserId], AccessChangeReasons.RolesAssigned, narrowed: taken.Count > 0, ct);
        return (await directory.ListMembersAsync([member.UserId], ct)).First(u => u.Id == member.UserId);
    }
}
