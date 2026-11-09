using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class UpdateUnitTypeValidator : AbstractValidator<UpdateUnitTypeCommand>
{
    public UpdateUnitTypeValidator() => RuleFor(x => x.Input).SetValidator(new UnitTypeInputValidator());
}
