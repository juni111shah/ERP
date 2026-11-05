using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Features;

namespace ERP.UnitTests;

public class HrWave4Tests
{
    private static StatutoryRule Flat(decimal rate, StatutoryBase b = StatutoryBase.Basic, decimal cap = 0, decimal threshold = 0, string? nationalities = null)
        => new() { Code = "SS", Name = "Social security", Method = StatutoryMethod.Flat, Base = b, Rate = rate, Cap = cap, Threshold = threshold, Nationalities = nationalities };

    [Fact]
    public void A_flat_rule_takes_a_percent_of_the_base_up_to_the_cap()
    {
        var r = Flat(5m, cap: 10_000m);
        Assert.Equal(150m, StatutoryMath.Amount(r, StatutoryMath.BaseFor(r, 3_000m, 4_000m)));
        Assert.Equal(500m, StatutoryMath.Amount(r, StatutoryMath.BaseFor(r, 25_000m, 30_000m))); // capped at 10 000
        Assert.Equal(200m, StatutoryMath.Amount(Flat(5m, StatutoryBase.Gross), StatutoryMath.BaseFor(Flat(5m, StatutoryBase.Gross), 3_000m, 4_000m)));
    }

    [Fact]
    public void Nothing_is_taken_below_the_threshold()
    {
        var r = Flat(5m, threshold: 2_000m);
        Assert.Equal(0m, StatutoryMath.Amount(r, 1_999m));
        Assert.Equal(100m, StatutoryMath.Amount(r, 2_000m));
    }

    [Fact]
    public void Slabs_tax_each_band_at_its_own_rate()
    {
        var r = new StatutoryRule { Method = StatutoryMethod.Slab, Slabs = [new() { FromAmount = 0, Rate = 0 }, new() { FromAmount = 1_000, Rate = 10 }, new() { FromAmount = 3_000, Rate = 20 }] };
        Assert.Equal(0m, StatutoryMath.Amount(r, 800m));
        Assert.Equal(100m, StatutoryMath.Amount(r, 2_000m));            // 1000 x 10%
        Assert.Equal(200m + 200m, StatutoryMath.Amount(r, 4_000m));     // 2000 x 10% + 1000 x 20%
    }

    [Theory]
    [InlineData(null, "Qatari", true)]
    [InlineData("", "Indian", true)]
    [InlineData("!Qatari", "Qatari", false)]
    [InlineData("!Qatari", "Indian", true)]
    [InlineData("Qatari, Omani", "omani", true)]
    [InlineData("Qatari, Omani", "Indian", false)]
    [InlineData("Qatari", null, false)]
    public void A_rule_applies_by_nationality(string? rule, string? nationality, bool expected)
        => Assert.Equal(expected, StatutoryMath.AppliesTo(rule, nationality));

    [Fact]
    public void A_rule_is_in_force_only_between_its_dates_and_when_active()
    {
        var r = new StatutoryRule { IsActive = true, EffectiveFrom = new DateOnly(2026, 6, 1), EffectiveTo = new DateOnly(2026, 12, 31) };
        Assert.False(StatutoryMath.InForce(r, new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31)));
        Assert.True(StatutoryMath.InForce(r, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30)));
        Assert.False(StatutoryMath.InForce(r, new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 31)));
        r.IsActive = false;
        Assert.False(StatutoryMath.InForce(r, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30)));
    }

    [Fact]
    public void Punches_fold_into_a_check_in_and_the_latest_check_out()
    {
        var first = PunchMath.Apply(null, null, new TimeOnly(8, 2));
        Assert.Equal((new TimeOnly(8, 2), (TimeOnly?)null), first);
        var second = PunchMath.Apply(first.CheckIn, first.CheckOut, new TimeOnly(17, 10));
        Assert.Equal(new TimeOnly(17, 10), second.CheckOut);
        var third = PunchMath.Apply(second.CheckIn, second.CheckOut, new TimeOnly(12, 0));   // a punch in the middle changes nothing
        Assert.Equal(new TimeOnly(17, 10), third.CheckOut);
        var same = PunchMath.Apply(new TimeOnly(8, 2), null, new TimeOnly(8, 2));            // a double tap
        Assert.Null(same.CheckOut);
        var early = PunchMath.Apply(new TimeOnly(9, 0), null, new TimeOnly(8, 0));           // an older punch arrives late
        Assert.Equal((new TimeOnly(8, 0), (TimeOnly?)new TimeOnly(9, 0)), early);
    }

    [Fact]
    public void A_day_counts_minutes_worked_late_arrival_and_overtime()
    {
        var shift = new TimeOnly(8, 0);
        var onTime = DailyAttendanceMath.Compute(new TimeOnly(8, 5), new TimeOnly(17, 30), shift, 10, 8);
        Assert.Equal(0, onTime.Late);                       // inside the grace period
        Assert.Equal(9 * 60 + 25, onTime.Worked);
        Assert.Equal(60 + 25, onTime.Overtime);             // beyond 8 hours
        var late = DailyAttendanceMath.Compute(new TimeOnly(8, 25), new TimeOnly(16, 0), shift, 10, 8);
        Assert.Equal(25, late.Late);                        // the whole delay counts once the grace is used up
        Assert.Equal(0, late.Overtime);
        Assert.Equal(new DailyAttendanceMath.Result(0, 0, 0), DailyAttendanceMath.Compute(null, null, shift, 10, 8));
    }

    [Fact]
    public void A_day_without_a_check_in_is_absent_unless_said_otherwise()
    {
        Assert.Equal(DailyAttendanceStatus.Absent, DailyAttendanceMath.StatusOf(null, null));
        Assert.Equal(DailyAttendanceStatus.Present, DailyAttendanceMath.StatusOf(null, new TimeOnly(8, 0)));
        Assert.Equal(DailyAttendanceStatus.Leave, DailyAttendanceMath.StatusOf(DailyAttendanceStatus.Leave, null));
    }

    private static EmployeeSnapshot Snap(decimal basic = 3_000m, decimal gross = 3_500m, string designation = "Agent", string? department = "Sales", Guid? dept = null, Guid? pos = null, Guid? mgr = null, ContractType? contract = null, EmployeeStatus status = EmployeeStatus.Active)
        => new(basic, gross, designation, department, dept, pos, mgr, contract, status);

    [Fact]
    public void History_records_pay_rises_promotions_and_transfers()
    {
        var before = Snap();
        var changes = HistoryMath.Diff(before, Snap(basic: 3_500m, gross: 4_000m, designation: "Senior Agent", department: "Leasing"));
        Assert.Contains(changes, c => c.Kind == HistoryKind.Salary && c.IsPay && c.Summary == "Basic salary increased" && c.Old == "3000.00" && c.New == "3500.00");
        Assert.Contains(changes, c => c.Kind == HistoryKind.Designation && c.New == "Senior Agent");
        Assert.Contains(changes, c => c.Kind == HistoryKind.Department && c.New == "Leasing");
        Assert.Empty(HistoryMath.Diff(before, Snap()));
        Assert.Single(HistoryMath.Diff(before, Snap(gross: 3_800m)), c => c.Summary == "Allowances changed");
        Assert.Empty(HistoryMath.Diff(before, Snap(status: EmployeeStatus.Terminated))); // leaving has its own entry
    }

    [Theory]
    [InlineData(45, null)]
    [InlineData(30, 30)]
    [InlineData(12, 30)]
    [InlineData(7, 7)]
    [InlineData(0, 7)]
    [InlineData(-1, null)]
    public void Contract_reminders_go_out_at_30_and_7_days(int daysLeft, int? stage)
        => Assert.Equal(stage, EmployeeContractReminders.StageFor(daysLeft));

    [Theory]
    [InlineData(CandidateStage.Applied, CandidateStage.Screening, true)]
    [InlineData(CandidateStage.Applied, CandidateStage.Interview, true)]
    [InlineData(CandidateStage.Interview, CandidateStage.Screening, false)]
    [InlineData(CandidateStage.Interview, CandidateStage.Rejected, true)]
    [InlineData(CandidateStage.Offer, CandidateStage.Hired, false)]      // only through the hire command
    [InlineData(CandidateStage.Hired, CandidateStage.Rejected, false)]
    [InlineData(CandidateStage.Rejected, CandidateStage.Applied, true)]
    [InlineData(CandidateStage.Rejected, CandidateStage.Interview, false)]
    public void Candidates_move_forward_or_out_and_hired_ones_stay(CandidateStage from, CandidateStage to, bool allowed)
        => Assert.Equal(allowed, CandidatePipeline.CanMove(from, to));
}
