using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class CreateUnitTypeValidator : AbstractValidator<CreateUnitTypeCommand>
{
    public CreateUnitTypeValidator() => RuleFor(x => x.Input).SetValidator(new UnitTypeInputValidator());
}
