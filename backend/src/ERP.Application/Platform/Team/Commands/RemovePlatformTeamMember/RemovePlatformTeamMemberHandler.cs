using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Platform;
using MediatR;

namespace ERP.Application.Platform;

internal sealed class RemovePlatformTeamMemberHandler(IPlatformDirectory directory, IRepository<PlatformSetting> settings, PlatformLog log, ICurrentUser user) : ICommandHandler<RemovePlatformTeamMemberCommand>
{
    public async Task<Unit> Handle(RemovePlatformTeamMemberCommand command, CancellationToken ct)
    {
        var email = (await directory.TeamAsync(ct)).FirstOrDefault(m => m.UserId == command.UserId)?.Email;
        await directory.RemoveTeamMemberAsync(command.UserId, user.UserId ?? throw new ForbiddenException(), ct);
        log.Add("Team member removed", null, null, email);
        await settings.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
