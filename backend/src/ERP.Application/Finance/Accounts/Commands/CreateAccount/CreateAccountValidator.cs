using FluentValidation;

namespace ERP.Application.Finance;

public sealed class CreateAccountValidator : AbstractValidator<CreateAccountCommand>
{
    public CreateAccountValidator() => RuleFor(x => x.Input).SetValidator(new AccountInputValidator());
}
