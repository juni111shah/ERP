using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Platform;
using MediatR;

namespace ERP.Application.Platform;

internal sealed class RemovePlatformTeamMemberHandler(IPlatformDirectory directory, IRepository<PlatformSetting> settings, PlatformLog log, ICurrentUser user, IAccessChangeNotifier accessChanges)
    : ICommandHandler<RemovePlatformTeamMemberCommand>
{
    public async Task<Unit> Handle(RemovePlatformTeamMemberCommand command, CancellationToken ct)
    {
        // 1. A manager can only be taken off the team by the owner, so one manager cannot lock the others out.
        var acting = user.UserId ?? throw new ForbiddenException();
        var member = (await directory.TeamAsync(ct)).FirstOrDefault(m => m.UserId == command.UserId);
        if (member is { Role: PlatformTeamRole.Manager } && await directory.PlatformRoleAsync(acting, ct) != PlatformTeamRole.Owner)
            throw new ForbiddenException("Only the platform owner can remove a manager.");

        // 2. Remove them; their open console screens close at once (the console checks the account on every request anyway).
        await directory.RemoveTeamMemberAsync(command.UserId, acting, ct);
        log.Add("Team member removed", null, null, member?.Email);
        await settings.SaveChangesAsync(ct);
        await accessChanges.PlatformTeamChangedAsync(command.UserId, AccessChangeReasons.PlatformTeamChanged, ct);
        return Unit.Value;
    }
}
