using ERP.Modules.RealEstate;

namespace ERP.UnitTests;

public class FundingScheduleTests
{
    [Fact]
    public void A_mortgage_is_an_annuity_with_the_documented_monthly_payment()
        => Assert.Equal(269_537.67m, FundingSchedule.MonthlyPayment(40_000_000m, 5.25m, 240));

    [Fact]
    public void A_zero_rate_plan_pays_equal_principal_and_no_interest()
    {
        var plan = FundingSchedule.Generate(7_600_000m, 0m, 24, new DateOnly(2026, 7, 1));
        Assert.Equal(24, plan.Count);
        Assert.Equal(316_666.67m, plan[0].Total);
        Assert.All(plan, p => Assert.Equal(0m, p.Interest));
        Assert.Equal(7_600_000m, plan.Sum(p => p.Principal));
        Assert.Equal(0m, plan[^1].BalanceAfter);
    }

    [Theory]
    [InlineData(40_000_000, 5.25, 240)]
    [InlineData(1_000_000, 7.1, 60)]
    [InlineData(123_456.78, 3.33, 37)]
    public void Principal_always_adds_up_and_the_balance_ends_at_zero(double principal, double rate, int months)
    {
        var plan = FundingSchedule.Generate((decimal)principal, (decimal)rate, months, new DateOnly(2026, 1, 31));
        Assert.Equal(months, plan.Count);
        Assert.Equal((decimal)principal, plan.Sum(p => p.Principal));
        Assert.Equal(0m, plan[^1].BalanceAfter);
        Assert.All(plan, p => Assert.Equal(p.Principal + p.Interest, p.Total));
        Assert.True(plan.Zip(plan.Skip(1)).All(x => x.First.BalanceAfter >= x.Second.BalanceAfter));
        Assert.Equal(new DateOnly(2026, 2, 28), plan[1].DueDate); // month-end start dates do not drift
    }

    [Fact]
    public void Interest_in_the_first_month_is_balance_times_monthly_rate()
    {
        var first = FundingSchedule.Generate(40_000_000m, 5.25m, 240, new DateOnly(2026, 7, 1))[0];
        Assert.Equal(175_000m, first.Interest);
        Assert.Equal(269_537.67m - 175_000m, first.Principal);
    }
}
