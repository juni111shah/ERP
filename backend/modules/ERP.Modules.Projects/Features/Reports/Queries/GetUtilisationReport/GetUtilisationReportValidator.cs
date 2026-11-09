using FluentValidation;

namespace ERP.Modules.Projects.Features;

public sealed class GetUtilisationReportValidator : AbstractValidator<GetUtilisationReportQuery>
{
    public GetUtilisationReportValidator()
    {
        RuleFor(x => x).Must(x => x.To >= x.From).WithName("to").WithMessage("The end date cannot be before the start date.");
        RuleFor(x => x).Must(x => x.To.DayNumber - x.From.DayNumber <= 400).WithName("to").WithMessage("Choose a period of at most 400 days.");
    }
}
