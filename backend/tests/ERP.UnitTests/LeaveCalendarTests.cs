using ERP.Modules.Hr.Features;

namespace ERP.UnitTests;

public class LeaveCalendarTests
{
    [Theory]
    [InlineData("Saturday,Sunday", new[] { DayOfWeek.Saturday, DayOfWeek.Sunday })]
    [InlineData("friday; saturday", new[] { DayOfWeek.Friday, DayOfWeek.Saturday })]
    [InlineData("  SUNDAY ", new[] { DayOfWeek.Sunday })]
    [InlineData("", new[] { DayOfWeek.Saturday, DayOfWeek.Sunday })]
    [InlineData("Funday", new[] { DayOfWeek.Saturday, DayOfWeek.Sunday })]
    public void The_weekend_setting_parses_names_and_falls_back_to_saturday_and_sunday(string text, DayOfWeek[] expected)
        => Assert.Equal(expected.OrderBy(d => d), LeaveCalendar.ParseWeekend(text).OrderBy(d => d));

    [Fact]
    public void Working_days_skip_the_weekend_and_holidays()
    {
        var holiday = new DateOnly(2031, 3, 5); // a Wednesday
        var calendar = new LeaveCalendar(LeaveCalendar.ParseWeekend(null), new HashSet<DateOnly> { holiday });
        Assert.Equal(4, calendar.Count(new DateOnly(2031, 3, 3), new DateOnly(2031, 3, 7))); // Mon-Fri less the holiday
        Assert.Equal(0, calendar.Count(new DateOnly(2031, 3, 8), new DateOnly(2031, 3, 9))); // Sat-Sun
        Assert.Equal(1, calendar.Count(new DateOnly(2031, 3, 3), new DateOnly(2031, 3, 3)));
    }

    [Fact]
    public void A_friday_saturday_weekend_changes_what_counts()
    {
        var calendar = new LeaveCalendar(LeaveCalendar.ParseWeekend("Friday,Saturday"), new HashSet<DateOnly>());
        Assert.Equal(5, calendar.Count(new DateOnly(2031, 3, 2), new DateOnly(2031, 3, 8))); // Sun-Thu count
    }
}
