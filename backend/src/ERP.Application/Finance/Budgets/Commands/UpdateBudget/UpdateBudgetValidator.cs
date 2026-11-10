using FluentValidation;

namespace ERP.Application.Finance;

public sealed class UpdateBudgetValidator : AbstractValidator<UpdateBudgetCommand>
{
    public UpdateBudgetValidator() => RuleFor(x => x.Input).SetValidator(new BudgetInputValidator());
}
