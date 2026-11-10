using FluentValidation;

namespace ERP.Modules.Hr.Features;

public sealed class GetHrSummaryReportValidator : AbstractValidator<GetHrSummaryReportQuery>
{
    public GetHrSummaryReportValidator()
    {
        RuleFor(x => x).Must(x => x.From is null || x.To is null || x.To >= x.From).WithName("to").WithMessage("The end date cannot be before the start date.");
        RuleFor(x => x).Must(x => x.From is null || x.To is null || x.To.Value.DayNumber - x.From.Value.DayNumber <= 366 * 5).WithName("to").WithMessage("Choose a period of at most five years.");
    }
}
