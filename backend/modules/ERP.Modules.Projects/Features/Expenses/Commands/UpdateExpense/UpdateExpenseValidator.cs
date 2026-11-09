using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class UpdateExpenseValidator : AbstractValidator<UpdateExpenseCommand>
{
    public UpdateExpenseValidator() => RuleFor(x => x.Input).SetValidator(new ExpenseInputValidator());
}
