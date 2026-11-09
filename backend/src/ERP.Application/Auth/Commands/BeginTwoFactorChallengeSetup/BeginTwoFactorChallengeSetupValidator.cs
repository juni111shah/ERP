using FluentValidation;

namespace ERP.Application.Auth;

public sealed class BeginTwoFactorChallengeSetupValidator : AbstractValidator<BeginTwoFactorChallengeSetupCommand>
{
    public BeginTwoFactorChallengeSetupValidator() => RuleFor(x => x.ChallengeToken).NotEmpty().MaximumLength(4000);
}
