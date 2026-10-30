using ERP.Application.Abstractions;
using ERP.Application.Dashboard;
using FluentValidation.TestHelper;

namespace ERP.UnitTests;

/// <summary>The overview period: default 12 months ending today, optional from/to, whole-period chart buckets and validation.</summary>
public class DashboardRangeTests
{
    private static readonly DateOnly Today = new(2026, 10, 30);
    private sealed class FixedClock(DateTime now) : IClock { public DateTime UtcNow => now; }
    private static GetDashboardValidator Validator() => new(new FixedClock(new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc)));

    [Fact]
    public void Missing_dates_default_to_the_12_months_ending_today()
    {
        Assert.Equal((new DateOnly(2025, 10, 31), Today), DashboardRange.Resolve(null, null, Today));
        Assert.Equal((new DateOnly(2025, 5, 1), new DateOnly(2026, 4, 30)), DashboardRange.Resolve(null, new DateOnly(2026, 4, 30), Today));
        Assert.Equal((new DateOnly(2026, 1, 1), Today), DashboardRange.Resolve(new DateOnly(2026, 1, 1), null, Today));
    }

    [Fact]
    public void A_scope_without_a_period_covers_the_last_year()
    {
        var scope = new DashboardScope(null, Today);
        Assert.Equal(new DateOnly(2025, 10, 31), scope.From);
        Assert.Equal(Today, scope.To);
    }

    [Theory]
    [InlineData(1, "2026-10-01")]   // 1M = 1 Oct..30 Oct (30 days)
    [InlineData(3, "2026-07-31")]
    [InlineData(6, "2026-05-01")]
    [InlineData(12, "2025-10-31")]  // 1Y = 365 days, not 367
    [InlineData(24, "2024-10-31")]
    [InlineData(36, "2023-10-31")]
    public void Presets_start_one_day_after_the_same_day_n_months_back(int months, string expected)
    {
        var from = DashboardRange.StartFor(Today, months);
        Assert.Equal(DateOnly.Parse(expected), from);
        if (months == 12) Assert.Equal(365, Today.DayNumber - from.DayNumber + 1);
        // month-end clamping: 31 Mar back one month is 28 Feb, so the period starts on 1 Mar
        Assert.Equal(new DateOnly(2026, 3, 1), DashboardRange.StartFor(new DateOnly(2026, 3, 31), 1));
    }

    [Fact]
    public void Granularity_is_weeks_up_to_3_months_months_up_to_2_years_then_quarters()
    {
        Assert.Equal("week", DashboardRange.Granularity(DashboardRange.StartFor(Today, 1), Today));
        Assert.Equal("week", DashboardRange.Granularity(DashboardRange.StartFor(Today, 3), Today));
        Assert.Equal("month", DashboardRange.Granularity(DashboardRange.StartFor(Today, 6), Today));
        Assert.Equal("month", DashboardRange.Granularity(DashboardRange.StartFor(Today, 24), Today));
        Assert.Equal("quarter", DashboardRange.Granularity(DashboardRange.StartFor(Today, 36), Today));
    }

    [Theory]
    [InlineData(1, 4)]    // 30 days: 4 weeks, the 2 leading days merged into the first week
    [InlineData(3, 13)]   // 92 days: 13 weeks, 1 leading day merged
    [InlineData(6, 6)]
    [InlineData(12, 12)]
    [InlineData(24, 24)]
    [InlineData(36, 12)]  // 12 quarters
    [InlineData(60, 20)]  // 5 years: 20 quarters
    public void Preset_buckets_are_whole_periods_with_no_stub(int months, int count)
    {
        var from = DashboardRange.StartFor(Today, months);
        var buckets = DashboardRange.Buckets(from, Today, DashboardRange.Granularity(from, Today));
        Assert.Equal(count, buckets.Count);
        Assert.Equal(from, buckets[0].From);
        Assert.Equal(Today, buckets[^1].To);
        for (var i = 1; i < buckets.Count; i++) Assert.Equal(buckets[i - 1].To.AddDays(1), buckets[i].From); // contiguous, no gaps or overlaps
    }

    [Fact]
    public void Twelve_month_buckets_follow_the_calendar_when_the_period_ends_at_a_month_end()
    {
        var to = new DateOnly(2026, 9, 30);
        var b = DashboardRange.Buckets(DashboardRange.StartFor(to, 12), to, "month");
        Assert.Equal(12, b.Count);
        Assert.Equal(new DashboardBucket(new DateOnly(2025, 10, 1), new DateOnly(2025, 10, 31)), b[0]);
        Assert.Equal(new DashboardBucket(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28)), b[4]);
        Assert.Equal("Oct 25", DashboardRange.Label(b[0], "month"));
    }

    [Fact]
    public void A_long_leading_remainder_stays_as_its_own_bucket()
    {
        // 15 Jan..30 Mar by month: 31 Jan..28 Feb is short of... counted back from 30 Mar: 1 Mar..30 Mar, 31 Jan..28 Feb, then 15..30 Jan (16 days, kept)
        var b = DashboardRange.Buckets(new DateOnly(2026, 1, 15), new DateOnly(2026, 3, 30), "month");
        Assert.Equal(3, b.Count);
        Assert.Equal(new DashboardBucket(new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 30)), b[0]);
        Assert.Single(DashboardRange.Buckets(Today, Today, "week"));
    }

    [Fact]
    public void Months_lists_every_calendar_month_the_period_touches()
    {
        Assert.Equal(13, DashboardRange.Months(new DateOnly(2025, 10, 30), Today).Count);
        Assert.Single(DashboardRange.Months(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 1)));
    }

    [Fact]
    public void The_validator_accepts_open_and_ordered_periods_up_to_5_years_and_rejects_the_rest()
    {
        var v = Validator();
        v.TestValidate(new GetDashboard(null)).ShouldNotHaveAnyValidationErrors();
        v.TestValidate(new GetDashboard(null, Today.AddMonths(-3), Today)).ShouldNotHaveAnyValidationErrors();
        v.TestValidate(new GetDashboard(null, Today, Today)).ShouldNotHaveAnyValidationErrors();
        v.TestValidate(new GetDashboard(null, DashboardRange.StartFor(Today, 60), Today)).ShouldNotHaveAnyValidationErrors(); // exactly 5 years
        v.TestValidate(new GetDashboard(null, Today.AddMonths(-1), Today.AddDays(1))).ShouldNotHaveAnyValidationErrors(); // a client ahead of UTC
        Assert.False(v.Validate(new GetDashboard(null, Today, Today.AddDays(-1))).IsValid);
        Assert.False(v.Validate(new GetDashboard(null, Today.AddYears(-5), Today)).IsValid); // 5 years and a day
        Assert.False(v.Validate(new GetDashboard(null, Today.AddYears(-11), Today)).IsValid);
        Assert.False(v.Validate(new GetDashboard(null, Today.AddYears(-6), null)).IsValid);   // missing "to" means today
        Assert.False(v.Validate(new GetDashboard(null, Today, Today.AddDays(2))).IsValid);    // future end date
        Assert.False(v.Validate(new GetDashboard(null, default(DateOnly), Today)).IsValid);
    }
}
