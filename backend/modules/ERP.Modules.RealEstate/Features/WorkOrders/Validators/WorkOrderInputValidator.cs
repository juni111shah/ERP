using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class WorkOrderInputValidator : AbstractValidator<WorkOrderInput>
{
    public WorkOrderInputValidator()
    {
        RuleFor(x => x.PropertyId).NotEmpty();
        RuleFor(x => x.Issue).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.EstimatedCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Category).MaximumLength(50);
    }
}
