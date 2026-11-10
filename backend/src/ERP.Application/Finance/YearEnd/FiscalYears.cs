namespace ERP.Application.Finance;

/// <summary>The fiscal calendar (pure). A fiscal year is named for the calendar year it ends in: with a start month of 4, year 2026 runs 1 Apr 2025 to 31 Mar 2026.</summary>
public static class FiscalYears
{
    public const string StartMonthSetting = "core.fiscalYearStartMonth";

    public static (DateOnly Start, DateOnly End) Range(int fiscalYear, int startMonth)
    {
        var month = startMonth is >= 1 and <= 12 ? startMonth : 1;
        var start = new DateOnly(month == 1 ? fiscalYear : fiscalYear - 1, month, 1);
        return (start, start.AddYears(1).AddDays(-1));
    }

    /// <summary>First day of the fiscal year that contains the date.</summary>
    public static DateOnly StartOf(DateOnly date, int startMonth)
    {
        var month = startMonth is >= 1 and <= 12 ? startMonth : 1;
        return new DateOnly(date.Month >= month ? date.Year : date.Year - 1, month, 1);
    }
}
