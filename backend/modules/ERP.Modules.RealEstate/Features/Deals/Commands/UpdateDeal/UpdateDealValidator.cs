using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class UpdateDealValidator : AbstractValidator<UpdateDealCommand>
{
    public UpdateDealValidator() => RuleFor(x => x.Input).SetValidator(new DealInputValidator());
}
