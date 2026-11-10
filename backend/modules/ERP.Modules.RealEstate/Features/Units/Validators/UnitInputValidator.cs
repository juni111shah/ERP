using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class UnitInputValidator : AbstractValidator<UnitInput>
{
    public UnitInputValidator()
    {
        RuleFor(x => x.PropertyId).NotEmpty();
        RuleFor(x => x.UnitTypeId).NotEmpty();
        RuleFor(x => x.UnitNumber).NotEmpty().MaximumLength(30);
        RuleFor(x => x.AreaSqft).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AskingRent).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Usage).IsInEnum().When(x => x.Usage is not null);
    }
}
