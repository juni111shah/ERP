using FluentValidation;

namespace ERP.Application.Finance;

public sealed class CreateInvoiceValidator : AbstractValidator<CreateInvoiceCommand>
{
    public CreateInvoiceValidator() => RuleFor(x => x.Input).SetValidator(new InvoiceInputValidator());
}
