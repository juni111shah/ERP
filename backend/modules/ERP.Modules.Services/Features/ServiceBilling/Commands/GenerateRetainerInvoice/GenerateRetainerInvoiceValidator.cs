using FluentValidation;

namespace ERP.Modules.Services.Features;

public sealed class GenerateRetainerInvoiceValidator : AbstractValidator<GenerateRetainerInvoiceCommand>
{
    public GenerateRetainerInvoiceValidator()
    {
        RuleFor(x => x.RetainerId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
