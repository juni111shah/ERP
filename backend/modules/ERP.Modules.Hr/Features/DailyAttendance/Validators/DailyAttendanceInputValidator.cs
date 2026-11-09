using ERP.Modules.Hr.Domain;
using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class DailyAttendanceInputValidator : AbstractValidator<DailyAttendanceInput>
{
    public DailyAttendanceInputValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(300);
        RuleFor(x => x.Source).MaximumLength(50);
        RuleFor(x => x.CheckOut!.Value).GreaterThan(x => x.CheckIn!.Value).When(x => x.CheckIn is not null && x.CheckOut is not null).WithName("CheckOut").WithMessage("Check-out must be after check-in.");
        RuleFor(x => x.CheckIn).Must((x, c) => c is null || x.Status is null or DailyAttendanceStatus.Present or DailyAttendanceStatus.HalfDay).WithMessage("Only a present or half day can have a check-in.");
    }
}
