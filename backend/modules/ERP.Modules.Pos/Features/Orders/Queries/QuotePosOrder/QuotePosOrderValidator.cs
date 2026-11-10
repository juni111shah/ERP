using FluentValidation;

namespace ERP.Modules.Pos.Features;

public class QuotePosOrderValidator : AbstractValidator<QuotePosOrderQuery>
{
    public QuotePosOrderValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("At least one item is required.");
        RuleForEach(x => x.Lines).ChildRules(PosLineRules.Lines);
        RuleFor(x => x.PromoCode).MaximumLength(50);
    }
}
