using ERP.Application.Common;

namespace ERP.Application.Auth;

internal sealed class LoginHandler(IAuthService auth) : ICommandHandler<LoginCommand, LoginResult>
{
    public Task<LoginResult> Handle(LoginCommand command, CancellationToken ct)
        => auth.LoginAsync(command.Email, command.Password, command.RememberMe, command.Ip, command.TwoFactorCode, ct);
}
