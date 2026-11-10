using FluentValidation;

namespace ERP.Application.Finance;

public sealed class CreateBudgetValidator : AbstractValidator<CreateBudgetCommand>
{
    public CreateBudgetValidator() => RuleFor(x => x.Input).SetValidator(new BudgetInputValidator());
}
