using ERP.Application.Finance;
using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class AdjustPayrollLineValidator : AbstractValidator<AdjustPayrollLineCommand>
{
    public AdjustPayrollLineValidator()
    {
        RuleFor(x => x.Note).MaximumLength(300);
        RuleFor(x => x.Adjustment).MustBeMoney();
    }
}
