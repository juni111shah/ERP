using FluentValidation;

namespace ERP.Modules.Crm.Features;

public class CreateLeadValidator : AbstractValidator<CreateLeadCommand>
{
    public CreateLeadValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty().WithMessage("Legal entity is required.");
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150).WithMessage("Full name is required.");
        RuleFor(x => x.EstimatedValue).GreaterThanOrEqualTo(0);
        When(x => !string.IsNullOrEmpty(x.Email), () =>
        {
            RuleFor(x => x.Email).EmailAddress().MaximumLength(150);
        });
    }
}
