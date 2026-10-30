namespace ERP.Modules.Hr.Features;

/// <summary>Working-day arithmetic for leave: weekends (from the setting) and public holidays do not count.</summary>
public sealed class LeaveCalendar(IReadOnlySet<DayOfWeek> weekend, IReadOnlySet<DateOnly> holidays)
{
    public static readonly DayOfWeek[] DefaultWeekend = [DayOfWeek.Saturday, DayOfWeek.Sunday];

    /// <summary>Parses "Saturday,Sunday" (names, any case, comma/semicolon separated). Unknown or empty input falls back to Saturday and Sunday.</summary>
    public static IReadOnlySet<DayOfWeek> ParseWeekend(string? text)
    {
        var set = new HashSet<DayOfWeek>();
        foreach (var part in (text ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (Enum.TryParse<DayOfWeek>(part, true, out var d)) set.Add(d);
        return set.Count > 0 ? set : DefaultWeekend.ToHashSet();
    }

    public bool IsWorkingDay(DateOnly d) => !weekend.Contains(d.DayOfWeek) && !holidays.Contains(d);

    /// <summary>Working days from..to inclusive.</summary>
    public int Count(DateOnly from, DateOnly to)
    {
        var n = 0;
        for (var d = from; d <= to; d = d.AddDays(1)) if (IsWorkingDay(d)) n++;
        return n;
    }
}
