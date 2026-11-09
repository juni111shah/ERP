using FluentValidation;

namespace ERP.Application.Organizations;

public sealed class LegalEntityInputValidator : AbstractValidator<LegalEntityInput>
{
    public LegalEntityInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CountryCode).NotEmpty().Length(2).WithMessage("Use a 2-letter ISO country code.");
        RuleFor(x => x.BaseCurrency).NotEmpty().Length(3).WithMessage("Use a 3-letter ISO currency code.");
        RuleFor(x => x.Address).MaximumLength(1000);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
    }
}
