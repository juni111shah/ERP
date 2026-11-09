using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class UpdateMilestoneValidator : AbstractValidator<UpdateMilestoneCommand>
{
    public UpdateMilestoneValidator() => RuleFor(x => x.Input).SetValidator(new MilestoneInputValidator());
}
