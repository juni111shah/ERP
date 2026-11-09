using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Domain.Identity;

namespace ERP.Application.Security;

internal sealed class InviteUserHandler(
    IRoleRepository roles, IUserDirectory directory, IOrganizationMemberRepository members, IUserRoleRepository userRoles, IRepository<UserLegalEntityAccess> legalEntityAccess,
    ILegalEntityRepository legalEntities, PermissionGrants grants, IUnitOfWork unitOfWork, ERP.Application.Platform.ISubscriptionLimits limits) : ICommandHandler<InviteUserCommand, UserAccount>
{
    public async Task<UserAccount> Handle(InviteUserCommand command, CancellationToken ct)
    {
        // 1. A free place on the plan, and only roles the caller may hand out.
        var i = command.Input;
        await limits.EnsureUserSlotAsync(ct);
        // Only roles / legal entities of this workspace count (the repositories apply the tenant filter).
        var roleIds = await roles.ListExistingIdsAsync(i.RoleIds, ct);
        await grants.EnsureCanAssignAsync(await grants.CallerGrantsAsync(ct), roleIds, ct); // before the account is created, so a refused invite leaves nothing behind

        // 2. The account, which must not be a member yet.
        var account = await directory.EnsureAccountAsync(i.Email.Trim(), i.FullName.Trim(), i.TemporaryPassword, ct);
        if (await members.AnyAsync(m => m.UserId == account.Id, ct)) throw new ConflictException("That user is already a member of this workspace.");

        // 3. Membership, roles and legal entities.
        members.Add(new OrganizationMember { UserId = account.Id, JoinedAt = DateTime.UtcNow });
        userRoles.AddRange(roleIds.Select(r => new UserRole { UserId = account.Id, RoleId = r }));
        if (i.LegalEntityIds is { Length: > 0 })
        {
            var entityIds = await legalEntities.ListExistingIdsAsync(i.LegalEntityIds, ct);
            legalEntityAccess.AddRange(entityIds.Select(e => new UserLegalEntityAccess { UserId = account.Id, LegalEntityId = e }));
        }
        await unitOfWork.SaveChangesAsync(ct);
        return account with { FullName = i.FullName.Trim() }; // accounts are global: an invite must not read back the name on an existing one
    }
}
