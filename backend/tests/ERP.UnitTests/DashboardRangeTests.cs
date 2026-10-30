using ERP.Application.Dashboard;
using FluentValidation.TestHelper;

namespace ERP.UnitTests;

/// <summary>The overview period: default last year, optional from/to, calendar-month buckets and validation.</summary>
public class DashboardRangeTests
{
    private static readonly DateOnly Today = new(2026, 10, 30);

    [Fact]
    public void Missing_dates_default_to_the_last_year_ending_today()
    {
        Assert.Equal((new DateOnly(2025, 10, 30), Today), DashboardRange.Resolve(null, null, Today));
        Assert.Equal((new DateOnly(2025, 4, 30), new DateOnly(2026, 4, 30)), DashboardRange.Resolve(null, new DateOnly(2026, 4, 30), Today));
        Assert.Equal((new DateOnly(2026, 1, 1), Today), DashboardRange.Resolve(new DateOnly(2026, 1, 1), null, Today));
    }

    [Fact]
    public void A_scope_without_a_period_covers_the_last_year()
    {
        var scope = new DashboardScope(null, Today);
        Assert.Equal(new DateOnly(2025, 10, 30), scope.From);
        Assert.Equal(Today, scope.To);
    }

    [Fact]
    public void Months_lists_every_calendar_month_the_period_touches()
    {
        var months = DashboardRange.Months(new DateOnly(2025, 10, 30), Today);
        Assert.Equal(13, months.Count);
        Assert.Equal(new DateOnly(2025, 10, 1), months[0]);
        Assert.Equal(new DateOnly(2026, 10, 1), months[^1]);
        Assert.Single(DashboardRange.Months(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 1)));
        Assert.Equal(37, DashboardRange.Months(Today.AddYears(-3), Today).Count);
    }

    [Fact]
    public void The_validator_accepts_open_and_ordered_periods_and_rejects_reversed_or_too_long_ones()
    {
        var v = new GetDashboardValidator();
        v.TestValidate(new GetDashboard(null)).ShouldNotHaveAnyValidationErrors();
        v.TestValidate(new GetDashboard(null, Today.AddMonths(-3), Today)).ShouldNotHaveAnyValidationErrors();
        v.TestValidate(new GetDashboard(null, Today, Today)).ShouldNotHaveAnyValidationErrors();
        Assert.False(v.Validate(new GetDashboard(null, Today, Today.AddDays(-1))).IsValid);
        Assert.False(v.Validate(new GetDashboard(null, Today.AddYears(-11), Today)).IsValid);
        Assert.False(v.Validate(new GetDashboard(null, default(DateOnly), Today)).IsValid);
    }
}
