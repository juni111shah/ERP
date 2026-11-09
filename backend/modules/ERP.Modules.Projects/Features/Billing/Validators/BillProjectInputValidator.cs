using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class BillProjectInputValidator : AbstractValidator<BillProjectInput>
{
    public BillProjectInputValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Grouping).Must(g => g is "detailed" or "person" or "task" or "project").WithMessage("Group by detailed, person, task or project.");
        RuleFor(x => x.DiscountPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.ProgressPercent).GreaterThan(0).LessThanOrEqualTo(100).When(x => x.ProgressPercent != null);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
