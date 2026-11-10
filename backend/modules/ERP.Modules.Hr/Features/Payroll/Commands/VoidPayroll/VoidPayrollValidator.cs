using ERP.Application.Finance;
using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class VoidPayrollValidator : AbstractValidator<VoidPayrollCommand>
{
    public VoidPayrollValidator() => RuleFor(x => x.Reason).ValidVoidReason();
}
