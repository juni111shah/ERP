using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class SaveBudgetLineValidator : AbstractValidator<SaveBudgetLineCommand>
{
    public SaveBudgetLineValidator()
    {
        RuleFor(x => x.Input.ProjectId).NotEmpty();
        RuleFor(x => x.Input.Category).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Input.Amount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Input.Hours).GreaterThanOrEqualTo(0);
    }
}
