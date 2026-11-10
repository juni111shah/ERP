using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class CreateDealValidator : AbstractValidator<CreateDealCommand>
{
    public CreateDealValidator() => RuleFor(x => x.Input).SetValidator(new DealInputValidator());
}
