using FluentValidation;

namespace ERP.Modules.Crm.Features;

public class CloseOpportunityValidator : AbstractValidator<CloseOpportunityCommand>
{
    public CloseOpportunityValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ActualRevenue).GreaterThanOrEqualTo(0);
    }
}
