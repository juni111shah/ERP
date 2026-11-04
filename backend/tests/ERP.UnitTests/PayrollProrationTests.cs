using ERP.Modules.Hr;

namespace ERP.UnitTests;

public class PayrollProrationTests
{
    [Theory]
    [InlineData(2026, 10, "2026-10-21", null, 11)]   // joins on the 21st of a 31-day month: 21st..31st
    [InlineData(2026, 10, "2020-01-01", "2026-10-10", 10)] // leaves on the 10th
    [InlineData(2026, 10, "2020-01-01", null, 31)]
    [InlineData(2026, 11, "2026-11-16", null, 15)]   // 30-day month
    [InlineData(2026, 2, "2026-02-15", null, 14)]    // 28-day month
    [InlineData(2026, 10, "2026-10-05", "2026-10-09", 5)] // joins and leaves in the same month
    [InlineData(2026, 10, "2026-10-20", "2026-10-10", 0)] // leaves before joining: nothing to pay
    [InlineData(2026, 10, "2026-11-01", null, 0)]    // joins after the month
    [InlineData(2026, 10, "2020-01-01", "2026-09-30", 0)] // left before the month
    public void Employed_days_count_calendar_days_inside_the_month(int y, int m, string join, string? leave, int expected)
        => Assert.Equal(expected, PayrollMath.EmployedDays(DateOnly.Parse(join), leave is null ? null : DateOnly.Parse(leave), y, m));

    [Fact]
    public void A_joiner_gets_basic_and_allowances_x_days_over_calendar_days()
    {
        var a = PayrollMath.Calculate(3_100m, 620m, 0m, 0, 0m, employedDays: 11, monthDays: 31);
        Assert.Equal(1_100m, a.Basic);
        Assert.Equal(220m, a.Allowances);
        Assert.Equal(1_320m, a.NetPay);
    }

    [Fact]
    public void Proration_uses_the_calendar_days_of_each_month()
    {
        Assert.Equal(2_000m, PayrollMath.Calculate(4_000m, 0m, 0m, 0, 0m, employedDays: 14, monthDays: 28).NetPay);
        Assert.Equal(1_000m, PayrollMath.Calculate(3_000m, 0m, 0m, 0, 0m, employedDays: 10, monthDays: 30).NetPay);
    }

    [Fact]
    public void Full_month_is_unchanged_overtime_is_not_prorated_and_absence_uses_the_full_monthly_gross()
    {
        var full = PayrollMath.Calculate(6_000m, 3_300m, 500m, 2, 10m, employedDays: 31, monthDays: 31);
        Assert.Equal(PayrollMath.Calculate(6_000m, 3_300m, 500m, 2, 10m), full);
        var part = PayrollMath.Calculate(6_000m, 3_300m, 0m, 1, 10m, employedDays: 10, monthDays: 30);
        Assert.Equal(312.50m, part.Overtime);          // the same overtime as a full month
        Assert.Equal(310m, part.Absence);              // 9,300 / 30 x 1, from the full gross
        Assert.Equal(2_000m + 1_100m - 310m + 312.50m, part.GrossEarnings);
    }

    [Fact]
    public void Changing_only_the_adjustment_keeps_the_other_parts()
    {
        var (gross, net) = PayrollMath.WithAdjustment(1_100m, 220m, 0m, 0m, 200m, 100m);
        Assert.Equal((1_520m, 1_420m), (gross, net));
    }
}
