using FluentValidation;

namespace ERP.Application.Finance;

public sealed class RegisterChequeValidator : AbstractValidator<RegisterChequeCommand>
{
    public RegisterChequeValidator() => RuleFor(x => x.Input).SetValidator(new ChequeInputValidator());
}
