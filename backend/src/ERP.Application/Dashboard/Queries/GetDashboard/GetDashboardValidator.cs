using ERP.Application.Abstractions;
using FluentValidation;

namespace ERP.Application.Dashboard;

public sealed class GetDashboardValidator : AbstractValidator<GetDashboardQuery>
{
    public GetDashboardValidator(IClock clock)
    {
        // "today" in UTC; one extra day lets clients ahead of UTC (e.g. Asia/Qatar, Asia/Karachi just after midnight) send their own today.
        DateOnly Today() => DateOnly.FromDateTime(clock.UtcNow);
        RuleFor(x => x.From).Must(f => f is null || f.Value != default).WithMessage("Enter a valid start date.");
        RuleFor(x => x.To).Must(t => t is null || t.Value != default).WithMessage("Enter a valid end date.");
        RuleFor(x => x).Must(x => x.From is not { } f || x.To is not { } t || f <= t).WithName("To").WithMessage("The end date must be on or after the start date.");
        RuleFor(x => x.To).Must(t => t is null || t.Value == default || t.Value <= Today().AddDays(1)).WithMessage("The end date can't be in the future.");
        RuleFor(x => x).Must(x => x.From is not { } f || f == default || DashboardRange.WithinMax(f, x.To ?? Today()))
            .WithName("From").WithMessage("The date range can be at most 5 years.");
    }
}
