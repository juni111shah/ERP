using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class ReviewCycleInputValidator : AbstractValidator<ReviewCycleInput>
{
    public ReviewCycleInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x).Must(x => x.PeriodEnd >= x.PeriodStart).WithName("periodEnd").WithMessage("The period must end on or after it starts.");
    }
}
