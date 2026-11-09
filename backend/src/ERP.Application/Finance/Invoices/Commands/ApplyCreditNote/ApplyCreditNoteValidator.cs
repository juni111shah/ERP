using FluentValidation;

namespace ERP.Application.Finance;

public sealed class ApplyCreditNoteValidator : AbstractValidator<ApplyCreditNoteCommand>
{
    public ApplyCreditNoteValidator() => RuleFor(x => x.Amount).GreaterThan(0).MustBeMoney();
}
