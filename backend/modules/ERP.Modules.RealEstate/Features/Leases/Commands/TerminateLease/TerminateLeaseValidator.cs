using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class TerminateLeaseValidator : AbstractValidator<TerminateLeaseCommand>
{
    public TerminateLeaseValidator() => RuleFor(x => x.Reason).NotEmpty().WithMessage("A termination reason is required.");
}
