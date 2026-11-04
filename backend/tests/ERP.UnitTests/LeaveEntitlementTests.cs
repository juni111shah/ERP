using ERP.Modules.Hr.Features;

namespace ERP.UnitTests;

public class LeaveEntitlementTests
{
    private static readonly (decimal, decimal)[] Tiers = [(5m, 28m)];
    private static bool Weekdays(DateOnly d) => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    [Fact]
    public void Service_years_are_whole_months_over_twelve()
    {
        Assert.Equal(5m, LeaveEntitlement.ServiceYears(new(2020, 1, 1), new(2025, 1, 1)));
        Assert.Equal(4m + 11m / 12m, LeaveEntitlement.ServiceYears(new(2020, 1, 2), new(2025, 1, 1)));
        Assert.Equal(0m, LeaveEntitlement.ServiceYears(new(2025, 1, 1), new(2020, 1, 1)));
    }

    [Fact]
    public void The_highest_reached_tier_applies_at_the_start_of_the_year()
    {
        Assert.Equal(21m, LeaveEntitlement.TierDays(21m, Tiers, new(2029, 1, 1), 2031)); // 2 years
        Assert.Equal(28m, LeaveEntitlement.TierDays(21m, Tiers, new(2025, 1, 1), 2031)); // 6 years
        Assert.Equal(28m, LeaveEntitlement.TierDays(21m, Tiers, new(2026, 1, 1), 2031)); // exactly 5 years
        Assert.Equal(21m, LeaveEntitlement.TierDays(21m, Tiers, new(2026, 6, 1), 2031)); // 4.58 years
        Assert.Equal(21m, LeaveEntitlement.TierDays(21m, [], new(2000, 1, 1), 2031));
        Assert.Equal(21m, LeaveEntitlement.TierDays(21m, Tiers, new(2031, 5, 1), 2031)); // join year: service at the join date is 0
    }

    [Fact]
    public void Pro_rata_in_the_join_year_rounds_to_half_a_day()
    {
        Assert.Equal(16m, LeaveEntitlement.ProRata(21m, new(2031, 4, 15), 2031));  // 21 x 9/12 = 15.75
        Assert.Equal(10.5m, LeaveEntitlement.ProRata(21m, new(2031, 7, 1), 2031)); // 6 months
        Assert.Equal(2m, LeaveEntitlement.ProRata(21m, new(2031, 12, 1), 2031));   // 1.75
    }

    [Fact]
    public void Pro_rata_leaves_other_years_alone()
    {
        Assert.Equal(21m, LeaveEntitlement.ProRata(21m, new(2020, 4, 1), 2031));
        Assert.Equal(0m, LeaveEntitlement.ProRata(21m, new(2032, 1, 1), 2031));
        Assert.Equal(21m, LeaveEntitlement.ProRata(21m, new(2031, 1, 20), 2031));
    }

    [Fact]
    public void Monthly_accrual_gives_a_twelfth_per_month()
    {
        Assert.Equal(3m, LeaveEntitlement.Accrued(12m, 3));
        Assert.Equal(12m, LeaveEntitlement.Accrued(12m, 12));
        Assert.Equal(0m, LeaveEntitlement.Accrued(12m, 0));
        Assert.Equal(2m, LeaveEntitlement.Accrued(21m, 1)); // 1.75 rounds to the nearest half day
    }

    [Fact]
    public void Accrued_months_follow_today_and_the_request_month()
    {
        Assert.Equal(12, LeaveEntitlement.AccruedMonths(2025, new(2026, 3, 1), null));
        Assert.Equal(3, LeaveEntitlement.AccruedMonths(2026, new(2026, 3, 1), null));
        Assert.Equal(6, LeaveEntitlement.AccruedMonths(2026, new(2026, 3, 1), new(2026, 6, 10)));
        Assert.Equal(5, LeaveEntitlement.AccruedMonths(2031, new(2026, 3, 1), new(2031, 5, 3)));
    }

    [Fact]
    public void Half_days_count_half_on_the_first_and_last_day()
    {
        var mon = new DateOnly(2031, 3, 3);
        Assert.Equal(0.5m, LeaveEntitlement.Split(mon, mon, true, false, Weekdays).Total);
        Assert.Equal(0.5m, LeaveEntitlement.Split(mon, mon, false, true, Weekdays).Total);
        Assert.Equal(0.5m, LeaveEntitlement.Split(mon, mon, true, true, Weekdays).Total);
        Assert.Equal(2.0m, LeaveEntitlement.Split(mon, mon.AddDays(2), true, true, Weekdays).Total);
        Assert.Equal(2.5m, LeaveEntitlement.Split(mon, mon.AddDays(2), true, false, Weekdays).Total);
        Assert.Equal(3m, LeaveEntitlement.Split(mon, mon.AddDays(2), false, false, Weekdays).Total);
    }

    [Fact]
    public void A_half_day_on_a_non_working_day_is_an_error()
    {
        var sat = new DateOnly(2031, 3, 8);
        Assert.NotNull(LeaveEntitlement.Split(sat, sat, true, false, Weekdays).Error);
        Assert.NotNull(LeaveEntitlement.Split(sat, sat.AddDays(2), true, false, Weekdays).Error);
        Assert.NotNull(LeaveEntitlement.Split(sat.AddDays(-2), sat, false, true, Weekdays).Error);
        Assert.Null(LeaveEntitlement.Split(sat, sat.AddDays(2), false, true, Weekdays).Error);
    }

    [Fact]
    public void A_request_across_new_year_is_split_per_calendar_year()
    {
        var r = LeaveEntitlement.Split(new(2032, 12, 27), new(2033, 1, 5), false, false, Weekdays);
        Assert.Equal(8m, r.Total);
        Assert.Equal(5m, r.ByYear[2032]);
        Assert.Equal(3m, r.ByYear[2033]);
        var half = LeaveEntitlement.Split(new(2032, 12, 27), new(2033, 1, 5), true, true, Weekdays);
        Assert.Equal(4.5m, half.ByYear[2032]);
        Assert.Equal(2.5m, half.ByYear[2033]);
    }
}
