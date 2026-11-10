using FluentValidation;

namespace ERP.Application.Finance;

public sealed class VoidInvoiceValidator : AbstractValidator<VoidInvoiceCommand>
{
    public VoidInvoiceValidator() => RuleFor(x => x.Reason).ValidVoidReason();
}
