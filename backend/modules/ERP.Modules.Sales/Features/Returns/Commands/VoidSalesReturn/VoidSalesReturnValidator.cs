using ERP.Application.Finance;
using FluentValidation;

namespace ERP.Modules.Sales.Features;

public sealed class VoidSalesReturnValidator : AbstractValidator<VoidSalesReturnCommand>
{
    public VoidSalesReturnValidator() => RuleFor(x => x.Reason).ValidVoidReason();
}
