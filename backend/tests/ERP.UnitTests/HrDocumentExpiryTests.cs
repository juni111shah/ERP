using ERP.Modules.Hr.Features;

namespace ERP.UnitTests;

public class HrDocumentExpiryTests
{
    private static readonly DateOnly Today = new(2026, 11, 4);

    [Theory]
    [InlineData(-1, null)]   // already expired: dashboard alert, no reminder
    [InlineData(0, 7)]
    [InlineData(7, 7)]
    [InlineData(8, 30)]
    [InlineData(30, 30)]
    [InlineData(31, 60)]
    [InlineData(60, 60)]
    [InlineData(61, null)]   // too far away
    public void The_reminder_stage_is_the_smallest_threshold_that_covers_the_days_left(int daysLeft, int? stage)
        => Assert.Equal(stage, DocumentExpiry.ReminderStage(daysLeft));

    [Fact]
    public void Days_left_counts_from_today_and_is_null_without_an_expiry()
    {
        Assert.Equal(10, DocumentExpiry.DaysLeft(Today.AddDays(10), Today));
        Assert.Equal(-3, DocumentExpiry.DaysLeft(Today.AddDays(-3), Today));
        Assert.Null(DocumentExpiry.DaysLeft(null, Today));
    }
}
