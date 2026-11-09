using ERP.Application.Finance;
using FluentValidation;

namespace ERP.Modules.RealEstate.Features;

public sealed class VoidOwnerPayoutValidator : AbstractValidator<VoidOwnerPayoutCommand>
{
    public VoidOwnerPayoutValidator() => RuleFor(x => x.Reason).ValidVoidReason();
}
