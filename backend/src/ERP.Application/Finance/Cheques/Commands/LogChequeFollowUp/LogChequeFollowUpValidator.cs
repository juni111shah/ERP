using FluentValidation;

namespace ERP.Application.Finance;

public sealed class LogChequeFollowUpValidator : AbstractValidator<LogChequeFollowUpCommand>
{
    public LogChequeFollowUpValidator() => RuleFor(x => x.Note).NotEmpty().MaximumLength(500);
}
