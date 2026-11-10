using FluentValidation;

namespace ERP.Application.Finance;

public sealed class UpdateJournalValidator : AbstractValidator<UpdateJournalCommand>
{
    public UpdateJournalValidator() => RuleFor(x => x.Input).SetValidator(new JournalInputValidator());
}
