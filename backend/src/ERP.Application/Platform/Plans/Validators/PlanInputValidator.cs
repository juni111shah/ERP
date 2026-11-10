using FluentValidation;

namespace ERP.Application.Platform;

public sealed class PlanInputValidator : AbstractValidator<PlanInput>
{
    public PlanInputValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Enter a plan code.").Matches("^[a-z0-9][a-z0-9-]{0,38}$").WithMessage("Use lower-case letters, digits or dashes.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Enter a plan name.").MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.MonthlyPrice).GreaterThanOrEqualTo(0).WithMessage("The price cannot be negative.");
        RuleFor(x => x.AnnualPrice).GreaterThanOrEqualTo(0).WithMessage("The price cannot be negative.");
        RuleFor(x => x.TrialDays).InclusiveBetween(0, 365).WithMessage("Use 0 to 365 days.");
        RuleFor(x => x.MaxUsers).GreaterThan(0).When(x => x.MaxUsers is not null).WithMessage("Leave empty for no limit, or use at least 1.");
        RuleFor(x => x.MaxLegalEntities).GreaterThan(0).When(x => x.MaxLegalEntities is not null).WithMessage("Leave empty for no limit, or use at least 1.");
    }
}
