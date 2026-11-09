using ERP.Modules.Services.Domain;

namespace ERP.Modules.Services.Features;

/// <summary>Retainer period arithmetic, anchored on the retainer's start date; no I/O.</summary>
public static class RetainerPeriods
{
    public static int MonthsIn(RetainerCycle cycle) => cycle switch { RetainerCycle.Quarterly => 3, RetainerCycle.Annual => 12, _ => 1 };

    /// <summary>The period holding a date. A date before the start or after the end of the retainer has none. The last period is cut short by the end date.</summary>
    public static (DateOnly Start, DateOnly End)? Containing(DateOnly start, DateOnly? end, RetainerCycle cycle, DateOnly date)
    {
        if (date < start || (end is { } last && date > last)) return null;
        var months = MonthsIn(cycle);
        var n = ((date.Year - start.Year) * 12 + date.Month - start.Month) / months;
        if (start.AddMonths(n * months) > date) n--;
        var periodEnd = start.AddMonths((n + 1) * months).AddDays(-1);
        return (start.AddMonths(n * months), end is { } e && e < periodEnd ? e : periodEnd);
    }

    /// <summary>Hours above the allowance, never below zero.</summary>
    public static decimal Overage(decimal billableHours, decimal includedHours) => Math.Max(0m, billableHours - includedHours);
}
