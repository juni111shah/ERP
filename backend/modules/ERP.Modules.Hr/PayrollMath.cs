namespace ERP.Modules.Hr;

public sealed record PayrollAmounts(decimal Basic, decimal Allowances, decimal Overtime, decimal Absence, decimal GrossEarnings, decimal Deductions, decimal NetPay);

/// <summary>
/// Pay rules: gross = basic + allowances; absence = gross ÷ days-basis × unpaid days; overtime = basic ÷ days-basis ÷ hours-per-day × multiplier × hours;
/// gross earnings = gross − absence + overtime + any manual adjustment; net = gross earnings − recurring deductions. All rounded to 2 dp (half away from zero).
/// </summary>
public static class PayrollMath
{
    public static PayrollAmounts Calculate(decimal basic, decimal allowances, decimal recurringDeductions, int unpaidDays, decimal overtimeHours, decimal adjustment = 0m,
        int daysBasis = 30, decimal overtimeMultiplier = 1.25m, int hoursPerDay = 8)
    {
        var gross = basic + allowances;
        var absence = R(gross / daysBasis * unpaidDays);
        var overtime = R(basic / daysBasis / hoursPerDay * overtimeMultiplier * overtimeHours);
        var earnings = gross - absence + overtime + adjustment;
        return new PayrollAmounts(basic, allowances, overtime, absence, earnings, recurringDeductions, earnings - recurringDeductions);
    }

    public static decimal R(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
