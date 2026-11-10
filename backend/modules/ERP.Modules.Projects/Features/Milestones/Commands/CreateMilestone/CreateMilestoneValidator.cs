using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class CreateMilestoneValidator : AbstractValidator<CreateMilestoneCommand>
{
    public CreateMilestoneValidator() => RuleFor(x => x.Input).SetValidator(new MilestoneInputValidator());
}
