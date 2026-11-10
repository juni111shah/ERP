using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class JobOpeningInputValidator : AbstractValidator<JobOpeningInput>
{
    public JobOpeningInputValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Headcount).InclusiveBetween(1, 500);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Status).IsInEnum();
    }
}
