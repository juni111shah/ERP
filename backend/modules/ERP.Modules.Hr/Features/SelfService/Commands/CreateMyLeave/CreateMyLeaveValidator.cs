using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class CreateMyLeaveValidator : AbstractValidator<CreateMyLeaveCommand>
{
    public CreateMyLeaveValidator()
    {
        RuleFor(x => x.Input.LeaveTypeId).NotEmpty().OverridePropertyName("Input.LeaveTypeId");
        RuleFor(x => x.Input.Reason).MaximumLength(500).OverridePropertyName("Input.Reason");
        RuleFor(x => x.Input).Must(x => x.ToDate >= x.FromDate).WithName("toDate").WithMessage("The leave must end on or after it starts.");
        RuleFor(x => x.Input).Must(x => x.ToDate.DayNumber - x.FromDate.DayNumber <= 365).WithName("toDate").WithMessage("A single request can cover at most a year.");
    }
}
