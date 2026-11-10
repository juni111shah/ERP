using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Application.Auth;

internal sealed class BeginTwoFactorSetupHandler(IAuthService auth, ICurrentUser user) : ICommandHandler<BeginTwoFactorSetupCommand, TwoFactorSetup>
{
    public Task<TwoFactorSetup> Handle(BeginTwoFactorSetupCommand command, CancellationToken ct) => auth.BeginTwoFactorSetupAsync(user.UserId ?? throw new ForbiddenException(), ct);
}
