using ERP.Application.Abstractions;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Auth;

internal sealed class ChangePasswordHandler(IAuthService auth, ICurrentUser user) : ICommandHandler<ChangePasswordCommand>
{
    public async Task<Unit> Handle(ChangePasswordCommand command, CancellationToken ct)
    {
        await auth.ChangePasswordAsync(user.UserId ?? throw new ForbiddenException(), command.CurrentPassword, command.NewPassword, ct);
        return Unit.Value;
    }
}
