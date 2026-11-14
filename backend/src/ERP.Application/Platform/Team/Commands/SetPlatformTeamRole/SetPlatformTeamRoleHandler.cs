using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

internal sealed class SetPlatformTeamRoleHandler(IPlatformDirectory directory, IRepository<PlatformSetting> settings, PlatformLog log, IAccessChangeNotifier accessChanges)
    : ICommandHandler<SetPlatformTeamRoleCommand, PlatformTeamMember>
{
    public async Task<PlatformTeamMember> Handle(SetPlatformTeamRoleCommand command, CancellationToken ct)
    {
        if (command.Role is not (PlatformTeamRole.Support or PlatformTeamRole.Manager)) throw new FieldValidationException("role", "Choose support or manager.");
        await directory.SetTeamRoleAsync(command.UserId, command.Role, ct);
        var member = (await directory.TeamAsync(ct)).First(m => m.UserId == command.UserId);
        log.Add("Team role changed", null, null, $"{member.Email} is now {(command.Role == PlatformTeamRole.Manager ? "a manager" : "support")}.");
        await settings.SaveChangesAsync(ct);
        // Their open console shows or hides the manager actions at once.
        await accessChanges.PlatformTeamChangedAsync(command.UserId, AccessChangeReasons.PlatformTeamChanged, ct);
        return member;
    }
}
