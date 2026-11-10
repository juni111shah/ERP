using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Features;

/// <summary>Report arithmetic; no I/O.</summary>
public static class ReportMath
{
    public static decimal Percent(decimal part, decimal whole) => whole == 0 ? 0m : Math.Round(part / whole * 100m, 1);

    /// <summary>The agreed fee a margin is measured against: a fixed-fee project always has one (the budget stands in until a contract value is set); any other project only when a contract value was entered.</summary>
    public static decimal ContractFee(Project p, IEnumerable<ProjectChangeOrder> changeOrders)
        => p.BillingType == ProjectBillingType.FixedFee || p.ContractValue != null ? ProjectMath.Fee(p, changeOrders) : 0m;

    private static readonly DayOfWeek[] MondayToFriday = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    /// <summary>The working days from the setting "Sun,Mon,Tue"; Monday to Friday when it is empty or unreadable.</summary>
    public static IReadOnlyCollection<DayOfWeek> ParseWorkWeek(string? text)
    {
        var days = (text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => Enum.GetValues<DayOfWeek>().Where(d => d.ToString().StartsWith(t.Length >= 3 ? t[..3] : "?", StringComparison.OrdinalIgnoreCase)).Cast<DayOfWeek?>().FirstOrDefault())
            .Where(d => d != null).Select(d => d!.Value).Distinct().ToList();
        return days.Count == 0 ? MondayToFriday : days;
    }

    public static int WorkdaysBetween(DateOnly from, DateOnly to, IReadOnlyCollection<DayOfWeek> week)
    {
        var n = 0;
        for (var d = from; d <= to; d = d.AddDays(1)) if (week.Contains(d.DayOfWeek)) n++;
        return n;
    }

    /// <summary>Share of the planned time that has passed, 0 to 1; a project without both dates has no plan to compare with.</summary>
    public static decimal? PlannedShare(DateOnly? start, DateOnly? end, DateOnly today)
    {
        if (start is not { } s || end is not { } e || e < s) return null;
        if (today <= s) return 0m;
        if (today >= e) return 1m;
        return (decimal)(today.DayNumber - s.DayNumber) / (e.DayNumber - s.DayNumber);
    }

    /// <summary>Working days (Monday to Friday) between two dates, inclusive; the unit of "capacity".</summary>
    public static int WeekdaysBetween(DateOnly from, DateOnly to)
    {
        var n = 0;
        for (var d = from; d <= to; d = d.AddDays(1)) if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) n++;
        return n;
    }

    /// <summary>Hours planned in the window for an allocation of HoursPerWeek between its dates (spread over weekdays).</summary>
    public static decimal AllocatedHours(DateOnly from, DateOnly to, DateOnly allocFrom, DateOnly allocTo, decimal hoursPerWeek)
    {
        var a = allocFrom > from ? allocFrom : from; var b = allocTo < to ? allocTo : to;
        return b < a ? 0m : Math.Round(WeekdaysBetween(a, b) * hoursPerWeek / 5m, 2);
    }

    /// <summary>Forecast at completion: spend so far divided by the share done; with nothing done yet the spend itself.</summary>
    public static decimal Forecast(decimal cost, decimal percentComplete) => percentComplete <= 0 ? cost : Math.Round(cost / (percentComplete / 100m), 2);

    /// <summary>Ageing bucket 0..3 by days since the date.</summary>
    public static int Bucket(DateOnly date, DateOnly asOf)
    {
        var days = asOf.DayNumber - date.DayNumber;
        return days <= 30 ? 0 : days <= 60 ? 1 : days <= 90 ? 2 : 3;
    }
}
