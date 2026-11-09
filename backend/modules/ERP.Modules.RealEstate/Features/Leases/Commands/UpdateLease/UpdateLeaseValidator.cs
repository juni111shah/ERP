using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class UpdateLeaseValidator : AbstractValidator<UpdateLeaseCommand>
{
    public UpdateLeaseValidator() => RuleFor(x => x.Input).SetValidator(new LeaseInputValidator());
}
