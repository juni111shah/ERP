using ERP.Application.Common;

namespace ERP.Application.Auth;

internal sealed class RefreshSessionHandler(IAuthService auth) : ICommandHandler<RefreshSessionCommand, SessionResult>
{
    public Task<SessionResult> Handle(RefreshSessionCommand command, CancellationToken ct) => auth.RefreshAsync(command.RefreshToken, command.Ip, ct);
}
