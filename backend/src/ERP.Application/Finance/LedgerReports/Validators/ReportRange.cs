using FluentValidation;

namespace ERP.Application.Finance;

/// <summary>Shared date-range rules of the ledger reports: real dates, start on or before end, at most 10 years (same cap as the party ledgers).</summary>
internal static class ReportRange
{
    public static void Apply<T>(AbstractValidator<T> v, Func<T, DateOnly?> from, Func<T, DateOnly> to)
    {
        v.RuleFor(x => to(x)).NotEqual(default(DateOnly)).WithName("To").WithMessage("Enter the end date.");
        v.RuleFor(x => from(x)).Must(f => f is null || f.Value != default).WithName("From").WithMessage("Enter a valid start date.");
        v.RuleFor(x => x).Must(x => from(x) is not { } f || f <= to(x)).WithName("To").WithMessage("The end date must be on or after the start date.");
        v.RuleFor(x => x).Must(x => from(x) is not { } f || to(x).DayNumber - f.DayNumber <= PartyLedger.MaxDays).WithName("To").WithMessage("The date range can be at most 10 years.");
    }
}
