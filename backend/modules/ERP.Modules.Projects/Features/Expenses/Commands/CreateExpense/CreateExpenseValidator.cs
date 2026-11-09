using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class CreateExpenseValidator : AbstractValidator<CreateExpenseCommand>
{
    public CreateExpenseValidator() => RuleFor(x => x.Input).SetValidator(new ExpenseInputValidator());
}
