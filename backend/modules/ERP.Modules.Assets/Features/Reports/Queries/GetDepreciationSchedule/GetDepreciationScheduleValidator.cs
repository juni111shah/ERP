using ERP.Application.Common;
using ERP.Application.Finance;
using FluentValidation;

namespace ERP.Modules.Assets.Features;

public sealed class GetDepreciationScheduleValidator : AbstractValidator<GetDepreciationScheduleQuery>
{
    public GetDepreciationScheduleValidator()
    {
        RuleFor(x => x.From).MustBePlausibleDate();
        RuleFor(x => x.To).MustBePlausibleDate();
        RuleFor(x => x).Must(x => x.From <= x.To).WithName("to").WithMessage("The end date must be on or after the start date.");
        RuleFor(x => x).Must(x => x.To.DayNumber - x.From.DayNumber <= PartyLedger.MaxDays).WithName("to").WithMessage("The date range can be at most 10 years.");
    }
}
