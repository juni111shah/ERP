using ERP.Application.Finance;
using FluentValidation;

namespace ERP.Modules.Purchasing.Features;

public sealed class VoidPurchaseReturnValidator : AbstractValidator<VoidPurchaseReturnCommand>
{
    public VoidPurchaseReturnValidator() => RuleFor(x => x.Reason).ValidVoidReason();
}
