using FluentValidation;

namespace ERP.Modules.Crm.Features;

public class UpdateLeadValidator : AbstractValidator<UpdateLeadCommand>
{
    public UpdateLeadValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.EstimatedValue).GreaterThanOrEqualTo(0);
    }
}
