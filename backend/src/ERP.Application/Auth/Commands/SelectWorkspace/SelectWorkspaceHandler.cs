using ERP.Application.Common;

namespace ERP.Application.Auth;

internal sealed class SelectWorkspaceHandler(IAuthService auth) : ICommandHandler<SelectWorkspaceCommand, SessionResult>
{
    public Task<SessionResult> Handle(SelectWorkspaceCommand command, CancellationToken ct)
        => auth.SelectWorkspaceAsync(command.SelectionToken, command.WorkspaceId, command.Ip, ct);
}
