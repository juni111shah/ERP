using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class WriteOffExpensesValidator : AbstractValidator<WriteOffExpensesCommand>
{
    public WriteOffExpensesValidator() => RuleFor(x => x.Ids).NotEmpty().Must(i => i.Count <= 500);
}
