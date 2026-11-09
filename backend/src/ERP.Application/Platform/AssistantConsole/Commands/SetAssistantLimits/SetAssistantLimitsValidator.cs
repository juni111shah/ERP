using FluentValidation;

namespace ERP.Application.Platform;

public sealed class SetAssistantLimitsValidator : AbstractValidator<SetAssistantLimitsCommand>
{
    private const int Min = 1_000;
    private const int Max = 100_000_000;

    public SetAssistantLimitsValidator()
    {
        RuleFor(x => x.Input.BrandDailyTokens).InclusiveBetween(Min, Max).WithMessage("Enter a number from 1,000 to 100,000,000.");
        RuleFor(x => x.Input.WorkspaceDailyTokens).InclusiveBetween(Min, Max).WithMessage("Enter a number from 1,000 to 100,000,000.");
    }
}
