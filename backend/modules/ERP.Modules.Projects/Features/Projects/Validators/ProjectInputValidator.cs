using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class ProjectInputValidator : AbstractValidator<ProjectInput>
{
    public ProjectInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Number).MaximumLength(30);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.CustomerName).MaximumLength(200);
        RuleFor(x => x.ManagerName).MaximumLength(150);
        RuleFor(x => x.BudgetAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ContractValue).GreaterThanOrEqualTo(0).When(x => x.ContractValue != null);
        RuleFor(x => x.BudgetHours).GreaterThanOrEqualTo(0).When(x => x.BudgetHours != null);
        RuleFor(x => x.SourceType).MaximumLength(60);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.BillingType).IsInEnum();
        RuleFor(x => x).Must(x => x.EndDate is null || x.StartDate is null || x.EndDate >= x.StartDate)
            .WithMessage("End date cannot be earlier than start date.");
    }
}
