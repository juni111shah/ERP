using ERP.Modules.RealEstate.Domain;
using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class SetWorkOrderCostBasisValidator : AbstractValidator<SetWorkOrderCostBasisCommand>
{
    public SetWorkOrderCostBasisValidator()
    {
        RuleFor(x => x.Input.ChargeableTo).IsInEnum().When(x => x.Input.ChargeableTo is not null);
        RuleFor(x => x.Input.OwnerSharePercent).InclusiveBetween(0m, 100m).When(x => x.Input.OwnerSharePercent is not null);
        RuleFor(x => x.Input.OwnerSharePercent).NotNull().When(x => x.Input.ChargeableTo == CostResponsibility.Shared).WithMessage("Enter the owner's share for a shared cost.");
    }
}
