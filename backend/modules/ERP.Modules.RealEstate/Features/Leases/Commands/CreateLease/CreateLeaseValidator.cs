using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class CreateLeaseValidator : AbstractValidator<CreateLeaseCommand>
{
    public CreateLeaseValidator() => RuleFor(x => x.Input).SetValidator(new LeaseInputValidator());
}
