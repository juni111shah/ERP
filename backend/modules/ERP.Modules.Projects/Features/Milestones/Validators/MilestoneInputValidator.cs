using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class MilestoneInputValidator : AbstractValidator<MilestoneInput>
{
    public MilestoneInputValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Status).IsInEnum();
    }
}
