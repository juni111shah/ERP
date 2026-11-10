using FluentValidation;

namespace ERP.Application.Finance;

public sealed class ReverseJournalValidator : AbstractValidator<ReverseJournalCommand>
{
    public ReverseJournalValidator() => RuleFor(x => x.Memo).MaximumLength(400);
}
