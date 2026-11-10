using ERP.Application.Common;
using FluentValidation;

namespace ERP.Application.Finance;

public sealed class ImportStatementValidator : AbstractValidator<ImportStatementCommand>
{
    public ImportStatementValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.Lines).NotNull().Must(l => l is { Count: > 0 and <= 2000 }).WithMessage("Import between 1 and 2000 lines at a time.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.Date).NotEqual(default(DateOnly)).WithMessage("Enter the statement line date.");
            l.RuleFor(x => x.Description).NotEmpty().MaximumLength(300);
            l.RuleFor(x => x.Reference).MaximumLength(100);
            l.RuleFor(x => x.Amount).NotEqual(0).MustBeMoney();
        });
    }
}
