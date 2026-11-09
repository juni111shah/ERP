using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class GetHoursReportValidator : AbstractValidator<GetHoursReportQuery>
{
    public GetHoursReportValidator()
    {
        RuleFor(x => x.GroupBy).Must(g => g is "person" or "project" or "task").WithMessage("Group by person, project or task.");
        RuleFor(x => x).Must(x => x.To >= x.From).WithName("to").WithMessage("The end date cannot be before the start date.");
    }
}
