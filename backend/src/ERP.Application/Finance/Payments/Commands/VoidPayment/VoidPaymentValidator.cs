using FluentValidation;

namespace ERP.Application.Finance;

public sealed class VoidPaymentValidator : AbstractValidator<VoidPaymentCommand>
{
    public VoidPaymentValidator() => RuleFor(x => x.Reason).ValidVoidReason();
}
