using FluentValidation;

namespace ERP.Modules.Pos.Features;

public class VoidPosOrderValidator : AbstractValidator<VoidPosOrderCommand>
{
    public VoidPosOrderValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("A reason is required to void an order.").MaximumLength(500);
    }
}
