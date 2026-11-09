using FluentValidation;

namespace ERP.Application.Printing;

public sealed class EmailDocumentValidator : AbstractValidator<EmailDocumentCommand>
{
    public EmailDocumentValidator() { RuleFor(x => x.Input.To).MaximumLength(254); RuleFor(x => x.Input.Message).MaximumLength(1000); }
}
