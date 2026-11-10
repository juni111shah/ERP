using FluentValidation;

namespace ERP.Application.Finance;

public sealed class CreateJournalValidator : AbstractValidator<CreateJournalCommand>
{
    public CreateJournalValidator() => RuleFor(x => x.Input).SetValidator(new JournalInputValidator());
}
