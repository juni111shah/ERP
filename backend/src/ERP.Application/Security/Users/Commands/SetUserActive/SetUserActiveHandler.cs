using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Application.Security;

internal sealed class SetUserActiveHandler(
    IOrganizationMemberRepository members, IUserRoleRepository userRoles, IUserDirectory directory, PermissionGrants grants, IUnitOfWork unitOfWork, ICurrentUser currentUser,
    ERP.Application.Auth.IAuthService auth, ERP.Application.Platform.ISubscriptionLimits limits) : ICommandHandler<SetUserActiveCommand, UserSummary>
{
    public async Task<UserSummary> Handle(SetUserActiveCommand command, CancellationToken ct)
    {
        // 1. Nobody deactivates themselves or the last administrator; reactivating needs a free place on the plan.
        var member = await members.FindByUserForUpdateAsync(command.UserId, ct) ?? throw new NotFoundException("That user is not a member of this workspace.");
        if (!command.IsActive)
        {
            if (command.UserId == currentUser.UserId) throw new ConflictException("You cannot deactivate your own account.");
            var adminRoleIds = await grants.AdminRoleIdsAsync(ct);
            if (await userRoles.AnyAsync(ur => ur.UserId == command.UserId && adminRoleIds.Contains(ur.RoleId), ct)) await grants.EnsureAnotherAdministratorAsync(command.UserId, adminRoleIds, ct);
        }
        if (command.IsActive && !member.IsActive) await limits.EnsureUserSlotAsync(ct);

        // 2. Switch, and sign a deactivated member out.
        member.IsActive = command.IsActive;
        await unitOfWork.SaveChangesAsync(ct);
        if (!command.IsActive) await auth.RevokeSessionsAsync(member.UserId, member.OrgId, "member-deactivated", ct);
        return (await directory.ListMembersAsync([member.UserId], ct)).First(u => u.Id == member.UserId);
    }
}
