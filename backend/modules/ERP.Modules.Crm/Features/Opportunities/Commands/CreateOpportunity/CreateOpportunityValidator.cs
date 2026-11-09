using FluentValidation;

namespace ERP.Modules.Crm.Features;

public class CreateOpportunityValidator : AbstractValidator<CreateOpportunityCommand>
{
    public CreateOpportunityValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty().WithMessage("Legal entity is required.");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.ExpectedRevenue).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ProbabilityPercent).InclusiveBetween(0, 100);
    }
}
