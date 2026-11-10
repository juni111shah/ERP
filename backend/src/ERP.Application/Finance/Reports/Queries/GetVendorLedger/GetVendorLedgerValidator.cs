using FluentValidation;

namespace ERP.Application.Finance;

public sealed class GetVendorLedgerValidator : AbstractValidator<GetVendorLedgerQuery>
{
    public GetVendorLedgerValidator()
    {
        RuleFor(x => x.PartyId).NotEmpty();
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("The end date must be on or after the start date.");
        RuleFor(x => x).Must(x => x.To.DayNumber - x.From.DayNumber <= PartyLedger.MaxDays).WithName("To").WithMessage("The date range can be at most 10 years.");
    }
}
