using FluentValidation;

namespace ERP.Application.Finance;

public sealed class JournalInputValidator : AbstractValidator<JournalInput>
{
    public JournalInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.Memo).MaximumLength(500);
        RuleFor(x => x.Reference).MaximumLength(100);
        RuleFor(x => x.Lines).NotNull().Must(l => l is { Count: >= 2 }).WithMessage("A journal needs at least two lines.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.AccountId).NotEmpty();
            l.RuleFor(x => x.Debit).GreaterThanOrEqualTo(0).MustBeMoney();
            l.RuleFor(x => x.Credit).GreaterThanOrEqualTo(0).MustBeMoney();
            l.RuleFor(x => x).Must(x => (x.Debit > 0) != (x.Credit > 0)).WithMessage("Each line needs either a debit or a credit.");
        });
    }
}
