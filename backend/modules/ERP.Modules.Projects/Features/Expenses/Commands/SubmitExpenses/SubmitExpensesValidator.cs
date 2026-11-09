using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class SubmitExpensesValidator : AbstractValidator<SubmitExpensesCommand>
{
    public SubmitExpensesValidator() => RuleFor(x => x.Ids).NotEmpty().Must(i => i.Count <= 500);
}
