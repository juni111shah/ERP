using FluentValidation;

namespace ERP.Application.Finance;

public sealed class VoidBillValidator : AbstractValidator<VoidBillCommand>
{
    public VoidBillValidator() => RuleFor(x => x.Reason).ValidVoidReason();
}
