using FluentValidation;

namespace ERP.Modules.Pos.Features;

public class GetPosSalesReportValidator : AbstractValidator<GetPosSalesReportQuery>
{
    public const int MaxDays = 366;

    public GetPosSalesReportValidator()
    {
        RuleFor(x => x).Must(x => x.From is null || x.To is null || x.To >= x.From).WithName("to").WithMessage("The end date must be on or after the start date.");
        RuleFor(x => x).Must(x => x.From is null || x.To is null || x.To.Value.DayNumber - x.From.Value.DayNumber < MaxDays).WithName("to").WithMessage("The report covers at most one year.");
    }
}
