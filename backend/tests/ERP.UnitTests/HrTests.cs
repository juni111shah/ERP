using ERP.Application.Templates;
using ERP.CountryPacks.AE;
using ERP.Modules.Hr;
using ERP.Modules.Hr.Features;

namespace ERP.UnitTests;

public class HrTests
{
    [Fact]
    public void Pay_is_gross_less_absence_plus_overtime_less_recurring_deductions()
    {
        // Basic 6,000 + allowances 3,300 = 9,300 gross; 2 unpaid days; 10 overtime hours; 500 recovery.
        var a = PayrollMath.Calculate(6_000m, 3_300m, 500m, unpaidDays: 2, overtimeHours: 10m);
        Assert.Equal(620m, a.Absence);          // 9,300 / 30 x 2
        Assert.Equal(312.50m, a.Overtime);      // 6,000 / 30 / 8 x 1.25 x 10
        Assert.Equal(9_300m - 620m + 312.50m, a.GrossEarnings);
        Assert.Equal(a.GrossEarnings - 500m, a.NetPay);
    }

    [Fact]
    public void Full_attendance_pays_the_full_gross_and_adjustments_move_the_total()
    {
        var plain = PayrollMath.Calculate(12_000m, 0m, 0m, 0, 0m);
        Assert.Equal(12_000m, plain.NetPay);
        var adjusted = PayrollMath.Calculate(12_000m, 0m, 0m, 0, 0m, adjustment: -250m);
        Assert.Equal(11_750m, adjusted.NetPay);
        Assert.Equal(1_000m, PayrollMath.Calculate(12_000m, 0m, 0m, 0, 0m, daysBasis: 30, hoursPerDay: 8, overtimeMultiplier: 1m, adjustment: -11_000m).NetPay);
    }

    [Fact]
    public void Absence_follows_the_configured_days_basis_and_rounds_half_away_from_zero()
        => Assert.Equal(333.33m, PayrollMath.Calculate(10_000m, 0m, 0m, 1, 0m).Absence);

    [Theory]
    [InlineData(1, 21, 4_200)]     // 6,000 / 30 x 21
    [InlineData(5, 105, 21_000)]
    [InlineData(7, 165, 33_000)]   // 105 + 2 x 30
    public void The_uae_rule_gives_21_days_a_year_for_five_years_then_30(int years, double days, double amount)
    {
        var calc = new GratuityCalculator(new CountryPackRegistry([new UaeCountryPack()]));
        var asOf = new DateOnly(2026, 10, 22);
        var g = calc.Calculate(6_000m, asOf.AddYears(-years).AddDays(-3), null, asOf, "AE"); // a few days past the anniversary: service is not an exact whole number of 365.25-day years
        Assert.True(g.Eligible);
        Assert.InRange(g.EntitlementDays, (decimal)days, (decimal)days + 1.5m);
        Assert.InRange(g.Amount, (decimal)amount, (decimal)amount + 300m);
    }

    [Fact]
    public void The_qatar_rule_gives_21_days_for_each_year_of_service()
    {
        var asOf = new DateOnly(2026, 10, 22);
        var g = new GratuityCalculator(new CountryPackRegistry([new ERP.CountryPacks.QA.QatarCountryPack()])).Calculate(6_000m, asOf.AddYears(-7).AddDays(-3), null, asOf, "QA");
        Assert.True(g.Eligible);
        Assert.InRange(g.EntitlementDays, 147m, 148.5m); // 7 x 21
    }

    [Fact]
    public void Under_a_year_of_service_there_is_no_entitlement()
    {
        var asOf = new DateOnly(2026, 10, 22);
        var g = new GratuityCalculator(new CountryPackRegistry([new UaeCountryPack()])).Calculate(6_000m, asOf.AddMonths(-6), null, asOf, "AE");
        Assert.Equal((false, 0m), (g.Eligible, g.Amount));
    }

    [Fact]
    public void A_country_without_a_rule_has_no_gratuity()
    {
        var g = new GratuityCalculator(new CountryPackRegistry([new UaeCountryPack()])).Calculate(6_000m, new DateOnly(2010, 1, 1), null, new DateOnly(2026, 1, 1), "PK");
        Assert.Equal((false, 0m), (g.Eligible, g.Amount));
    }

    [Fact]
    public void An_employee_is_on_the_payroll_from_the_join_month_until_the_termination_month()
    {
        var e = new ERP.Modules.Hr.Domain.Employee { JoinDate = new DateOnly(2026, 3, 15), TerminationDate = new DateOnly(2026, 6, 10), Status = ERP.Modules.Hr.Domain.EmployeeStatus.Terminated };
        DateOnly S(int m) => new(2026, m, 1); DateOnly E(int m) => S(m).AddMonths(1).AddDays(-1);
        Assert.False(e.IsPayableIn(S(2), E(2)));
        Assert.True(e.IsPayableIn(S(3), E(3)));
        Assert.True(e.IsPayableIn(S(6), E(6)));
        Assert.False(e.IsPayableIn(S(7), E(7)));
    }
}
