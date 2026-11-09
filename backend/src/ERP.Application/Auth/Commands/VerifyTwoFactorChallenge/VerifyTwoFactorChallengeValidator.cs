using FluentValidation;

namespace ERP.Application.Auth;

public sealed class VerifyTwoFactorChallengeValidator : AbstractValidator<VerifyTwoFactorChallengeCommand>
{
    public VerifyTwoFactorChallengeValidator()
    {
        RuleFor(x => x.ChallengeToken).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
    }
}
