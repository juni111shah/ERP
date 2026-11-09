using ERP.Application.Finance;
using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class TerminateInputValidator : AbstractValidator<TerminateInput>
{
    public TerminateInputValidator()
    {
        RuleFor(x => x.TerminationDate).MustBePlausibleDate();
        RuleFor(x => x.Reason).MaximumLength(300);
        RuleFor(x => x.LastWorkingDay!.Value).LessThanOrEqualTo(x => x.TerminationDate).When(x => x.LastWorkingDay is not null).WithName("LastWorkingDay").WithMessage("The last working day cannot be after the termination date.");
    }
}
