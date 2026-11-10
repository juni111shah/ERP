using FluentValidation;

namespace ERP.Application.Finance;

public sealed class UpdateAccountValidator : AbstractValidator<UpdateAccountCommand>
{
    public UpdateAccountValidator() => RuleFor(x => x.Input).SetValidator(new AccountInputValidator());
}
