using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class PmScheduleInputValidator : AbstractValidator<PmScheduleInput>
{
    public PmScheduleInputValidator()
    {
        RuleFor(x => x.PropertyId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.FrequencyMonths).InclusiveBetween(1, 120);
        RuleFor(x => x.LeadDays).InclusiveBetween(0, 365);
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.EstimatedCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Category).MaximumLength(50);
    }
}
