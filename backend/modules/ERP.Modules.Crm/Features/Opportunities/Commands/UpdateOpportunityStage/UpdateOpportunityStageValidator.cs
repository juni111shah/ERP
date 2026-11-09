using FluentValidation;

namespace ERP.Modules.Crm.Features;

public class UpdateOpportunityStageValidator : AbstractValidator<UpdateOpportunityStageCommand>
{
    public UpdateOpportunityStageValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ProbabilityPercent).InclusiveBetween(0, 100);
    }
}
