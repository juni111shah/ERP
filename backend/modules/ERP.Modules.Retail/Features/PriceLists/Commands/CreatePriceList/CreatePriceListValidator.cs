using FluentValidation;

namespace ERP.Modules.Retail.Features;

public class CreatePriceListValidator : AbstractValidator<CreatePriceListCommand>
{
    public CreatePriceListValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.Currency).MaximumLength(10);
    }
}
