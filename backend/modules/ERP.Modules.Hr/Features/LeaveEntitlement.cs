namespace ERP.Modules.Hr.Features;

/// <summary>Pure leave arithmetic: service-based tiers, first-year pro-rata, monthly accrual, half days and the year-end split. No I/O so it is easy to test.</summary>
public static class LeaveEntitlement
{
    public static decimal RoundToHalf(decimal value) => Math.Round(value * 2m, MidpointRounding.AwayFromZero) / 2m;

    /// <summary>Completed service in years (whole months / 12) between two dates; never negative.</summary>
    public static decimal ServiceYears(DateOnly joined, DateOnly asOf)
    {
        if (asOf <= joined) return 0m;
        var months = (asOf.Year - joined.Year) * 12 + asOf.Month - joined.Month - (asOf.Day < joined.Day ? 1 : 0);
        return Math.Max(0, months) / 12m;
    }

    /// <summary>Full-year days for the year: the highest tier reached at 1 January (at the join date in the join year), else the type's annual days.</summary>
    public static decimal TierDays(decimal annualDays, IReadOnlyCollection<(decimal FromServiceYears, decimal Days)> tiers, DateOnly joined, int year)
    {
        var reference = joined.Year >= year ? joined : new DateOnly(year, 1, 1);
        var years = ServiceYears(joined, reference);
        var hit = tiers.Where(t => t.FromServiceYears <= years).OrderByDescending(t => t.FromServiceYears).Select(t => ((decimal, decimal)?)t).FirstOrDefault();
        return hit?.Item2 ?? annualDays;
    }

    /// <summary>In the join year: days x months remaining (the join month counts) / 12 rounded to the nearest half day. Other years are unchanged; before joining there is nothing.</summary>
    public static decimal ProRata(decimal fullYearDays, DateOnly joined, int year)
    {
        if (joined.Year > year) return 0m;
        if (joined.Year < year) return fullYearDays;
        return RoundToHalf(fullYearDays * (13 - joined.Month) / 12m);
    }

    public static decimal Entitlement(decimal annualDays, IReadOnlyCollection<(decimal FromServiceYears, decimal Days)> tiers, bool proRataFirstYear, DateOnly joined, int year)
    {
        var days = TierDays(annualDays, tiers, joined, year);
        return proRataFirstYear ? ProRata(days, joined, year) : days;
    }

    /// <summary>Months of the year already accrued: all 12 for past years; for the current year the month of today or of the request's last day, whichever is later; the request's month for a future year.</summary>
    public static int AccruedMonths(int year, DateOnly today, DateOnly? requestEndInYear)
    {
        if (year < today.Year) return 12;
        var months = year == today.Year ? today.Month : 0;
        if (requestEndInYear is { } end && end.Year == year) months = Math.Max(months, end.Month);
        return months;
    }

    /// <summary>Monthly accrual: the entitlement x months / 12 rounded to the nearest half day (never above the entitlement).</summary>
    public static decimal Accrued(decimal entitled, int months) => months >= 12 ? entitled : Math.Min(entitled, RoundToHalf(entitled * Math.Max(0, months) / 12m));

    /// <summary>Weight of each calendar day of the request: 1 on a working day, 0 otherwise; the first/last day is 0.5 with a half-day flag. Fails when a half day falls on a non-working day.</summary>
    public static (Dictionary<int, decimal> ByYear, decimal Total, string? Error) Split(DateOnly from, DateOnly to, bool startHalf, bool endHalf, Func<DateOnly, bool> isWorking)
    {
        var byYear = new Dictionary<int, decimal>();
        decimal total = 0;
        if ((startHalf && !isWorking(from)) || (endHalf && !isWorking(to)))
            return (byYear, 0, "A half day must fall on a working day (not a weekend or public holiday).");
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            if (!isWorking(d)) continue;
            var w = 1m;
            if ((d == from && startHalf) || (d == to && endHalf)) w = 0.5m;
            total += w;
            byYear[d.Year] = byYear.GetValueOrDefault(d.Year) + w;
        }
        return (byYear, total, null);
    }
}
