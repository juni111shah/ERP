using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Auth;

internal sealed class ResetPasswordHandler(IAuthService auth) : ICommandHandler<ResetPasswordCommand>
{
    public async Task<Unit> Handle(ResetPasswordCommand command, CancellationToken ct)
    {
        await auth.ResetPasswordAsync(command.Token, command.NewPassword, ct);
        return Unit.Value;
    }
}
