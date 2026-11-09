using FluentValidation;

namespace ERP.Modules.Pos.Features;

public class CreatePosOrderValidator : AbstractValidator<CreatePosOrderCommand>
{
    public CreatePosOrderValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.RegisterId).NotEmpty();
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("At least one item is required.");
        RuleForEach(x => x.Lines).ChildRules(PosLineRules.Lines);
        RuleFor(x => x.PromoCode).MaximumLength(50);
        RuleFor(x => x.Payments).NotEmpty().WithMessage("At least one payment tender is required.");
        RuleForEach(x => x.Payments).ChildRules(p =>
        {
            p.RuleFor(x => x.Amount).GreaterThan(0);
            p.RuleFor(x => x.Method).IsInEnum();
        });
    }
}
