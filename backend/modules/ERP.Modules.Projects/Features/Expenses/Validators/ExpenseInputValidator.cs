using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class ExpenseInputValidator : AbstractValidator<ExpenseInput>
{
    public ExpenseInputValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Category).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Amount).GreaterThan(0).LessThan(1_000_000_000m);
        RuleFor(x => x.MarkupPercent).InclusiveBetween(0, 500);
        RuleFor(x => x.WorkerName).MaximumLength(100);
    }
}
