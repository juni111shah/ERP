using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class CreateUnitValidator : AbstractValidator<CreateUnitCommand>
{
    public CreateUnitValidator() => RuleFor(x => x.Input).SetValidator(new UnitInputValidator());
}
