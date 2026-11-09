using ERP.Application.Common;

namespace ERP.Application.Auth;

internal sealed class BeginTwoFactorChallengeSetupHandler(IAuthService auth) : ICommandHandler<BeginTwoFactorChallengeSetupCommand, TwoFactorChallengeSetup>
{
    public Task<TwoFactorChallengeSetup> Handle(BeginTwoFactorChallengeSetupCommand command, CancellationToken ct) => auth.BeginChallengeSetupAsync(command.ChallengeToken, ct);
}
