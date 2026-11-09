using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class CreatePropertyValidator : AbstractValidator<CreatePropertyCommand>
{
    public CreatePropertyValidator() => RuleFor(x => x.Input).SetValidator(new PropertyInputValidator());
}
