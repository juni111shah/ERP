namespace ERP.Modules.Hr.Features;

/// <summary>Pay bands of a sick-type leave: from day N of the year (counted per employee) the day pays X% of the daily rate. No band for a day = full pay.</summary>
public static class LeavePayBands
{
    /// <summary>Share of the daily rate paid for day number <paramref name="dayOfYear"/> (1 = the first leave day of the year) of the type.</summary>
    public static decimal PaidFraction(IReadOnlyCollection<(int FromDay, decimal PayPercent)> bands, int dayOfYear)
    {
        var hit = bands.Where(b => b.FromDay <= dayOfYear).OrderByDescending(b => b.FromDay).Select(b => (decimal?)b.PayPercent).FirstOrDefault();
        return Math.Clamp((hit ?? 100m) / 100m, 0m, 1m);
    }
}
