using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class SaveAttendanceValidator : AbstractValidator<SaveAttendanceCommand>
{
    public SaveAttendanceValidator()
    {
        RuleFor(x => x.Input.EmployeeId).NotEmpty();
        RuleFor(x => x.Input.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Input.Month).InclusiveBetween(1, 12);
        RuleFor(x => x.Input.WorkingDays).InclusiveBetween(1, 31);
        RuleFor(x => x.Input.DaysPresent).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Input.UnpaidAbsentDays).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Input.PaidLeaveDays).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Input.OvertimeHours).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Input).Must(i => i.DaysPresent <= i.WorkingDays).WithName("daysPresent").WithMessage("Days present cannot exceed the working days.");
        RuleFor(x => x.Input).Must(i => i.UnpaidAbsentDays + i.PaidLeaveDays <= i.WorkingDays).WithName("unpaidAbsentDays").WithMessage("Absence and leave cannot exceed the working days.");
    }
}
