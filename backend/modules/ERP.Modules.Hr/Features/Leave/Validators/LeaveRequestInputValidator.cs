using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class LeaveRequestInputValidator : AbstractValidator<LeaveRequestInput>
{
    public LeaveRequestInputValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.LeaveTypeId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500);
        RuleFor(x => x).Must(x => x.ToDate >= x.FromDate).WithName("toDate").WithMessage("The leave must end on or after it starts.");
        RuleFor(x => x).Must(x => x.ToDate.DayNumber - x.FromDate.DayNumber <= 365).WithName("toDate").WithMessage("A single request can cover at most a year.");
    }
}
