using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Application.Auth;

internal sealed class SwitchWorkspaceHandler(IAuthService auth, ICurrentUser user) : ICommandHandler<SwitchWorkspaceCommand, SessionResult>
{
    public Task<SessionResult> Handle(SwitchWorkspaceCommand command, CancellationToken ct)
        => auth.SwitchWorkspaceAsync(user.UserId ?? throw new ForbiddenException(), user.OrgId ?? throw new ForbiddenException(), command.WorkspaceId,
            command.CurrentJti, command.CurrentExp, command.Persistent, command.Ip, ct);
}
