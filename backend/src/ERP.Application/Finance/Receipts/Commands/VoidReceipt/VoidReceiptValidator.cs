using FluentValidation;

namespace ERP.Application.Finance;

public sealed class VoidReceiptValidator : AbstractValidator<VoidReceiptCommand>
{
    public VoidReceiptValidator() => RuleFor(x => x.Reason).ValidVoidReason();
}
