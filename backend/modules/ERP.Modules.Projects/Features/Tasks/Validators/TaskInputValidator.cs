using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class TaskInputValidator : AbstractValidator<TaskInput>
{
    public TaskInputValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.AssignedTo).MaximumLength(100);
        RuleFor(x => x.Labels).MaximumLength(300);
        RuleFor(x => x.EstimatedHours).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ActualHours).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x).Must(x => x.StartDate is null || x.DueDate is null || x.DueDate >= x.StartDate).WithName("dueDate").WithMessage("The due date cannot be before the start date.");
    }
}
