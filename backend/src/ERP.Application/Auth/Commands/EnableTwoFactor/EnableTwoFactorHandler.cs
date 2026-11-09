using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Application.Auth;

internal sealed class EnableTwoFactorHandler(IAuthService auth, ICurrentUser user) : ICommandHandler<EnableTwoFactorCommand, TwoFactorEnabled>
{
    public async Task<TwoFactorEnabled> Handle(EnableTwoFactorCommand command, CancellationToken ct)
        => new(await auth.EnableTwoFactorAsync(user.UserId ?? throw new ForbiddenException(), command.Code, ct));
}
