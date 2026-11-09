using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class ChangeWorkOrderStatusValidator : AbstractValidator<ChangeWorkOrderStatusCommand>
{
    public ChangeWorkOrderStatusValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.ActualCost).GreaterThanOrEqualTo(0).When(x => x.ActualCost is not null);
    }
}
