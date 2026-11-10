using ERP.Application.Common;

namespace ERP.Application.Auth;

internal sealed class VerifyTwoFactorChallengeHandler(IAuthService auth) : ICommandHandler<VerifyTwoFactorChallengeCommand, TwoFactorChallengeResult>
{
    public Task<TwoFactorChallengeResult> Handle(VerifyTwoFactorChallengeCommand command, CancellationToken ct)
        => auth.VerifyChallengeAsync(command.ChallengeToken, command.Code, command.Ip, ct);
}
