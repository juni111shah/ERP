using ERP.Application.Abstractions;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Auth;

internal sealed class DisableTwoFactorHandler(IAuthService auth, ICurrentUser user) : ICommandHandler<DisableTwoFactorCommand>
{
    public async Task<Unit> Handle(DisableTwoFactorCommand command, CancellationToken ct)
    {
        await auth.DisableTwoFactorAsync(user.UserId ?? throw new ForbiddenException(), command.Password, command.Code, ct);
        return Unit.Value;
    }
}
