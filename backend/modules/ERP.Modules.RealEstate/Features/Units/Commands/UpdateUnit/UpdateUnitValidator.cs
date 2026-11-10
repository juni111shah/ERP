using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class UpdateUnitValidator : AbstractValidator<UpdateUnitCommand>
{
    public UpdateUnitValidator() => RuleFor(x => x.Input).SetValidator(new UnitInputValidator());
}
