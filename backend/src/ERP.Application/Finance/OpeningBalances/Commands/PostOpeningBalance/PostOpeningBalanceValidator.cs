using FluentValidation;

namespace ERP.Application.Finance;

public sealed class PostOpeningBalanceValidator : AbstractValidator<PostOpeningBalanceCommand>
{
    public PostOpeningBalanceValidator()
    {
        RuleFor(x => x.Input.LegalEntityId).NotEmpty();
        RuleFor(x => x.Input.Date).MustBePlausibleDate();
        RuleFor(x => x.Input.Memo).MaximumLength(400);
        RuleFor(x => x.Input.Lines).NotNull().Must(l => l is { Count: > 0 }).WithMessage("Enter at least one opening balance.");
        RuleForEach(x => x.Input.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.AccountId).NotEmpty();
            l.RuleFor(x => x.Debit).GreaterThanOrEqualTo(0).MustBeMoney();
            l.RuleFor(x => x.Credit).GreaterThanOrEqualTo(0).MustBeMoney();
            l.RuleFor(x => x).Must(x => (x.Debit > 0) != (x.Credit > 0)).WithMessage("Each line needs either a debit or a credit.");
            l.RuleFor(x => x.Description).MaximumLength(400);
            l.RuleFor(x => x.Reference).MaximumLength(100);
            l.RuleFor(x => x.DueDate).MustBePlausibleDate();
        });
    }
}
