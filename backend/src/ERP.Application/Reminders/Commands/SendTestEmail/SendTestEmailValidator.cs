using FluentValidation;

namespace ERP.Application.Reminders;

public sealed class SendTestEmailValidator : AbstractValidator<SendTestEmailCommand>
{
    public SendTestEmailValidator() => RuleFor(x => x.To).NotEmpty().EmailAddress().WithMessage("Enter a valid email address.");
}
