using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class InspectionInputValidator : AbstractValidator<InspectionInput>
{
    public InspectionInputValidator()
    {
        RuleFor(x => x.PropertyId).NotEmpty();
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Inspector).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(4000);
        RuleFor(x => x.Items).NotNull().Must(i => i is null || i.Count <= 200).WithMessage("At most 200 checklist items.");
        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(x => x.Area).NotEmpty().MaximumLength(100);
            i.RuleFor(x => x.Condition).IsInEnum();
            i.RuleFor(x => x.RepairCost).GreaterThanOrEqualTo(0);
            i.RuleFor(x => x.Notes).MaximumLength(1000);
        });
    }
}
