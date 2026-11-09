using FluentValidation;

namespace ERP.Application.Finance;

public sealed class CreatePaymentValidator : AbstractValidator<CreatePaymentCommand>
{
    public CreatePaymentValidator() => RuleFor(x => x.Input).SetValidator(new PaymentInputValidator());
}
