using FluentValidation;

namespace ERP.Application.Finance;

public sealed class AccountInputValidator : AbstractValidator<AccountInput>
{
    public AccountInputValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.CurrencyCode).Length(3).When(x => !string.IsNullOrEmpty(x.CurrencyCode));
    }
}
