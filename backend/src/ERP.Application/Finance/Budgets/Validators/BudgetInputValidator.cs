using FluentValidation;

namespace ERP.Application.Finance;

public sealed class BudgetInputValidator : AbstractValidator<BudgetInput>
{
    public BudgetInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.FiscalYear).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.Lines).NotNull().Must(l => l is null || l.Count <= 2000).WithMessage("A budget can have at most 2000 lines.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.AccountId).NotEmpty();
            l.RuleFor(x => x.Month).InclusiveBetween(1, 12);
            l.RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
        });
        RuleFor(x => x.Lines).Must(l => l is null || l.Select(x => (x.AccountId, x.Month)).Distinct().Count() == l.Count).WithMessage("Each account can appear once per month.");
    }
}
