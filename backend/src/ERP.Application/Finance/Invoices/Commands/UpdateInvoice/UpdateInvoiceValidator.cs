using FluentValidation;

namespace ERP.Application.Finance;

public sealed class UpdateInvoiceValidator : AbstractValidator<UpdateInvoiceCommand>
{
    public UpdateInvoiceValidator() => RuleFor(x => x.Input).SetValidator(new InvoiceInputValidator());
}
