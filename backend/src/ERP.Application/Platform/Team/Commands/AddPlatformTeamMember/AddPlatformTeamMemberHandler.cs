using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

internal sealed class AddPlatformTeamMemberHandler(IPlatformDirectory directory, IRepository<PlatformSetting> settings, PlatformLog log, ICurrentUser user, IAccessChangeNotifier accessChanges)
    : ICommandHandler<AddPlatformTeamMemberCommand, NewTeamMember>
{
    public async Task<NewTeamMember> Handle(AddPlatformTeamMemberCommand command, CancellationToken ct)
    {
        // 1. Only the owner hands out the manager role.
        var role = command.Input.Role;
        if (role == PlatformTeamRole.Owner) throw new FieldValidationException("role", "Choose support or manager.");
        if (role == PlatformTeamRole.Manager && await directory.PlatformRoleAsync(user.UserId ?? throw new ForbiddenException(), ct) != PlatformTeamRole.Owner)
            throw new ForbiddenException("Only the platform owner can make someone a manager.");

        // 2. Add them; an account that is signed in somewhere sees the console at once.
        var added = await directory.AddTeamMemberAsync(command.Input.Email.Trim(), command.Input.FullName.Trim(), role, ct);
        log.Add("Team member added", null, null, $"{added.Member.Email} ({added.Member.Role.ToString().ToLowerInvariant()})");
        await settings.SaveChangesAsync(ct);
        await accessChanges.PlatformTeamChangedAsync(added.Member.UserId, AccessChangeReasons.PlatformTeamChanged, ct);
        return added;
    }
}
