using FluentValidation;

namespace ERP.Modules.Services.Features;

public class CreateMilestoneValidator : AbstractValidator<CreateMilestoneCommand>
{
    public CreateMilestoneValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty().WithMessage("Legal entity is required.");
        RuleFor(x => x.EngagementId).NotEmpty().WithMessage("Engagement is required.");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Amount must be greater than zero.");
    }
}
