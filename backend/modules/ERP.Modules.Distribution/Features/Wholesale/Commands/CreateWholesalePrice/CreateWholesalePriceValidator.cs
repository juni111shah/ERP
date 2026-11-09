using FluentValidation;

namespace ERP.Modules.Distribution.Features;

public class CreateWholesalePriceValidator : AbstractValidator<CreateWholesalePriceCommand>
{
    public CreateWholesalePriceValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.MinQuantity).GreaterThan(0);
        RuleFor(x => x.WholesaleUnitPrice).GreaterThan(0);
    }
}
