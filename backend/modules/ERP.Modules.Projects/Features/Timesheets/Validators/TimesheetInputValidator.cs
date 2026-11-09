using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class TimesheetInputValidator : AbstractValidator<TimesheetInput>
{
    public TimesheetInputValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.WorkerName).NotEmpty().When(x => x.WorkerUserId is null).WithMessage("Say who did the work.");
        RuleFor(x => x.WorkerName).MaximumLength(100);
        RuleFor(x => x.Hours).GreaterThan(0).LessThanOrEqualTo(24).WithMessage("Hours must be between 0.1 and 24.");
        RuleFor(x => x.HourlyRate).GreaterThanOrEqualTo(0).When(x => x.HourlyRate != null);
        RuleFor(x => x.CostRate).GreaterThanOrEqualTo(0).When(x => x.CostRate != null);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
