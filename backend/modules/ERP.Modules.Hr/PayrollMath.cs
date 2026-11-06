namespace ERP.Modules.Hr;

public sealed record PayrollAmounts(decimal Basic, decimal Allowances, decimal Overtime, decimal Absence, decimal GrossEarnings, decimal Deductions, decimal NetPay);

/// <summary>
/// Pay rules: gross = basic + allowances; absence = gross ÷ days-basis × unpaid days; overtime = basic ÷ days-basis ÷ hours-per-day × multiplier × hours;
/// gross earnings = gross − absence + overtime + any manual adjustment; net = gross earnings − recurring deductions. All rounded to 2 dp (half away from zero).
/// </summary>
public static class PayrollMath
{
    public static PayrollAmounts Calculate(decimal basic, decimal allowances, decimal recurringDeductions, decimal unpaidDays, decimal overtimeHours, decimal adjustment = 0m,
        int daysBasis = 30, decimal overtimeMultiplier = 1.25m, int hoursPerDay = 8, int employedDays = 0, int monthDays = 0)
    {
        var gross = basic + allowances;
        var absence = R(gross / daysBasis * unpaidDays);
        var overtime = R(basic / daysBasis / hoursPerDay * overtimeMultiplier * overtimeHours);
        // Joiners and leavers: basic and allowances x employed days / calendar days (employedDays 0 = no proration). Absence and overtime use the full monthly figures.
        var prorate = employedDays > 0 && monthDays > 0 && employedDays < monthDays;
        var paidBasic = prorate ? R(basic * employedDays / monthDays) : basic;
        var paidAllowances = prorate ? R(allowances * employedDays / monthDays) : allowances;
        var earnings = paidBasic + paidAllowances - absence + overtime + adjustment;
        return new PayrollAmounts(paidBasic, paidAllowances, overtime, absence, earnings, recurringDeductions, earnings - recurringDeductions);
    }

    /// <summary>Calendar days of the month the person is employed: from the later of the join date and the 1st to the earlier of the termination date and the last day. 0 when they never overlap.</summary>
    public static int EmployedDays(DateOnly joinDate, DateOnly? terminationDate, int year, int month)
    {
        var start = new DateOnly(year, month, 1); var end = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        var from = joinDate > start ? joinDate : start;
        var to = terminationDate is { } t && t < end ? t : end;
        return to < from ? 0 : to.DayNumber - from.DayNumber + 1;
    }

    /// <summary>Earnings and net of a line whose parts are already fixed, after changing only the manual adjustment. Terminal pay is the leave encashment and end of service of a final settlement.</summary>
    public static (decimal Gross, decimal Net) WithAdjustment(decimal basic, decimal allowances, decimal overtime, decimal absence, decimal adjustment, decimal deductions, decimal terminalPay = 0m)
    {
        var earnings = basic + allowances - absence + overtime + adjustment + terminalPay;
        return (earnings, earnings - deductions);
    }

    public static decimal R(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
