using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class UpdatePropertyValidator : AbstractValidator<UpdatePropertyCommand>
{
    public UpdatePropertyValidator() => RuleFor(x => x.Input).SetValidator(new PropertyInputValidator());
}
