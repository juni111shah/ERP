namespace ERP.Modules.Hr.Features;

/// <summary>How close a document is to expiring. The dashboard, the list and the reminders all use these same thresholds.</summary>
public static class DocumentExpiry
{
    public static readonly int[] ReminderDays = [60, 30, 7];

    public static int? DaysLeft(DateOnly? expiry, DateOnly today) => expiry is { } e ? e.DayNumber - today.DayNumber : null;

    /// <summary>The reminder stage a document is in: 60, 30 or 7 (the smallest threshold that still covers the days left); null when it is further away or already expired.</summary>
    public static int? ReminderStage(int daysLeft) => daysLeft < 0 ? null : ReminderDays.Where(d => daysLeft <= d).Cast<int?>().Min();
}
