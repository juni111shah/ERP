using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Auth;

internal sealed class LogoutHandler(IAuthService auth) : ICommandHandler<LogoutCommand>
{
    public async Task<Unit> Handle(LogoutCommand command, CancellationToken ct)
    {
        await auth.LogoutAsync(command.RefreshToken, command.Jti, command.Exp, ct);
        return Unit.Value;
    }
}
