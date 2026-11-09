using FluentValidation;

namespace ERP.Modules.Retail.Features;

public class UpdatePromotionValidator : AbstractValidator<UpdatePromotionCommand>
{
    public UpdatePromotionValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.DiscountValue).GreaterThan(0);
    }
}
