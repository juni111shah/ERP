using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Auth;

internal sealed class ForgotPasswordHandler(IAuthService auth) : ICommandHandler<ForgotPasswordCommand>
{
    public async Task<Unit> Handle(ForgotPasswordCommand command, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(command.Email)) await auth.ForgotPasswordAsync(command.Email, ct); // never reveals whether the email has an account
        return Unit.Value;
    }
}
