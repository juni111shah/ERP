using FluentValidation;

namespace ERP.Modules.Crm.Features;

public class ConvertLeadValidator : AbstractValidator<ConvertLeadCommand>
{
    public ConvertLeadValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.OpportunityTitle).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ExpectedRevenue).GreaterThanOrEqualTo(0);
    }
}
