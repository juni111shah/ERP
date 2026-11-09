using FluentValidation;

namespace ERP.Application.Printing;

public sealed class EmailPartyStatementValidator : AbstractValidator<EmailPartyStatementCommand>
{
    public EmailPartyStatementValidator() { RuleFor(x => x.Input.To).MaximumLength(254); RuleFor(x => x.Input.Message).MaximumLength(1000); }
}
