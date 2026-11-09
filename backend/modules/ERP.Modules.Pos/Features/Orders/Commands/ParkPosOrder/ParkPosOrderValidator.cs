using FluentValidation;

namespace ERP.Modules.Pos.Features;

public class ParkPosOrderValidator : AbstractValidator<ParkPosOrderCommand>
{
    public ParkPosOrderValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("At least one item is required.");
        RuleForEach(x => x.Lines).ChildRules(PosLineRules.Lines);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}
