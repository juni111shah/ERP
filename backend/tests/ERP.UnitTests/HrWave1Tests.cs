using ERP.Modules.Hr;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Features;

namespace ERP.UnitTests;

public class HrWave1Tests
{
    [Theory]
    [InlineData(1_000, 3_000, 2_000, 1_000)]   // normal instalment
    [InlineData(1_000, 400, 2_000, 400)]       // never more than the balance left
    [InlineData(1_000, 3_000, 250, 250)]       // never more than the pay that is left
    [InlineData(1_000, 3_000, -50, 0)]         // nothing when the pay is already negative
    public void A_loan_recovery_is_capped_by_balance_and_pay(double instalment, double remaining, double available, double expected)
        => Assert.Equal((decimal)expected, LoanMath.Recovery((decimal)instalment, (decimal)remaining, (decimal)available));

    [Fact]
    public void Three_instalments_of_a_3000_loan_are_1000_each_and_start_on_the_chosen_month()
    {
        Assert.Equal(1_000m, LoanMath.DefaultInstalment(3_000m, 3));
        var loan = new EmployeeLoan { StartYear = 2026, StartMonth = 10 };
        Assert.False(LoanMath.Started(loan, 2026, 9));
        Assert.True(LoanMath.Started(loan, 2026, 10));
        Assert.True(LoanMath.Started(loan, 2027, 1));
    }

    [Fact]
    public void Unused_leave_counts_what_is_earned_so_far_plus_carried_less_taken()
    {
        Assert.Equal(21m, SettlementMath.EncashableDays(30m, 0m, 9m, monthlyAccrual: false, terminationMonth: 3));
        Assert.Equal(7.5m - 2m + 3m, SettlementMath.EncashableDays(30m, 3m, 2m, monthlyAccrual: true, terminationMonth: 3)); // 30 x 3/12 = 7.5 earned
        Assert.Equal(0m, SettlementMath.EncashableDays(10m, 0m, 25m, false, 6));
    }

    [Fact]
    public void Notice_shortfall_is_the_notice_period_less_the_days_served()
    {
        var term = new DateOnly(2026, 11, 30);
        Assert.Equal(20, SettlementMath.NoticeShortfallDays(30, new DateOnly(2026, 11, 20), term));
        Assert.Equal(0, SettlementMath.NoticeShortfallDays(30, new DateOnly(2026, 10, 1), term));
        Assert.Equal(0, SettlementMath.NoticeShortfallDays(30, null, term));     // no notice date: counted as served
        Assert.Equal(0, SettlementMath.NoticeShortfallDays(null, new DateOnly(2026, 11, 20), term));
    }

    [Fact]
    public void The_settlement_loan_recovery_never_exceeds_the_balance_or_the_pay()
    {
        Assert.Equal(500m, SettlementMath.LoanRecovery(500m, 9_000m));
        Assert.Equal(900m, SettlementMath.LoanRecovery(2_000m, 900m));
        Assert.Equal(0m, SettlementMath.LoanRecovery(2_000m, -10m));
    }

    [Fact]
    public void Sick_leave_bands_pay_full_then_half_then_nothing()
    {
        (int, decimal)[] bands = [(1, 100m), (16, 50m), (31, 0m)];
        Assert.Equal(1m, LeavePayBands.PaidFraction(bands, 1));
        Assert.Equal(1m, LeavePayBands.PaidFraction(bands, 15));
        Assert.Equal(0.5m, LeavePayBands.PaidFraction(bands, 16));
        Assert.Equal(0.5m, LeavePayBands.PaidFraction(bands, 30));
        Assert.Equal(0m, LeavePayBands.PaidFraction(bands, 31));
        Assert.Equal(1m, LeavePayBands.PaidFraction([(10, 50m)], 5)); // before the first band: full pay
    }

    [Fact]
    public void Half_pay_days_deduct_half_a_day_of_pay()
    {
        var a = PayrollMath.Calculate(3_000m, 0m, 0m, unpaidDays: 0.5m, overtimeHours: 0m);
        Assert.Equal(50m, a.Absence); // 3000 / 30 x 0.5
    }
}
