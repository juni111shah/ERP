using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class LeadInputValidator : AbstractValidator<LeadInput>
{
    public LeadInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Phone).MaximumLength(40);
        RuleFor(x => x.Email).MaximumLength(150).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Source).MaximumLength(60);
        RuleFor(x => x.Interest).MaximumLength(300);
        RuleFor(x => x.Notes).MaximumLength(4000);
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Phone) || !string.IsNullOrWhiteSpace(x.Email)).WithMessage("Enter a phone number or an email so the lead can be contacted.");
    }
}
