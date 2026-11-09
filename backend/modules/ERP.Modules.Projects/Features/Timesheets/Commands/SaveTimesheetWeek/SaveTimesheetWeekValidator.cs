using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class SaveTimesheetWeekValidator : AbstractValidator<SaveTimesheetWeekCommand>
{
    public SaveTimesheetWeekValidator()
    {
        RuleFor(x => x.Items).NotEmpty().Must(i => i.Count <= 200).WithMessage("Save at most 200 entries at a time.");
        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(x => x.ProjectId).NotEmpty();
            i.RuleFor(x => x.Hours).InclusiveBetween(0, 24);
            i.RuleFor(x => x.WorkerName).MaximumLength(100);
            i.RuleFor(x => x.Description).MaximumLength(500);
        });
    }
}
