using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class ListingInputValidator : AbstractValidator<ListingInput>
{
    public ListingInputValidator()
    {
        RuleFor(x => x.PropertyId).NotEmpty();
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.Channels).MaximumLength(300);
        RuleFor(x => x.Description).MaximumLength(4000);
    }
}
