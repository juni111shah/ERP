using ERP.Domain.Common;
using ERP.Modules.RealEstate;
using ERP.Modules.RealEstate.Domain;
using Unit = ERP.Modules.RealEstate.Domain.Unit;

namespace ERP.UnitTests;

public class RentScheduleTests
{
    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    [Fact]
    public void Quarterly_twelve_months_gives_four_equal_instalments_due_on_the_first_day_of_each_period()
    {
        var s = RentSchedule.Generate(D(2026, 1, 1), D(2026, 12, 31), 98_000m, 4);
        Assert.Equal(4, s.Count);
        Assert.All(s, i => Assert.Equal(24_500m, i.Amount));
        Assert.Equal([D(2026, 1, 1), D(2026, 4, 1), D(2026, 7, 1), D(2026, 10, 1)], s.Select(i => i.DueDate));
        Assert.Equal(D(2026, 3, 31), s[0].PeriodTo);
        Assert.Equal(D(2026, 12, 31), s[^1].PeriodTo);
        Assert.Equal([1, 2, 3, 4], s.Select(i => i.Sequence));
    }

    [Fact]
    public void Rounding_remainder_goes_to_the_last_instalment_so_the_total_is_exact()
    {
        var s = RentSchedule.Generate(D(2026, 1, 1), D(2026, 12, 31), 98_000m, 12);
        Assert.Equal(12, s.Count);
        Assert.All(s.Take(11), i => Assert.Equal(8_166.67m, i.Amount));
        Assert.Equal(8_166.63m, s[^1].Amount); // spec example: 12 x 8,166.67 would overshoot, the last row absorbs it
        Assert.Equal(98_000m, RentSchedule.TermRent(s));
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(4)] [InlineData(6)] [InlineData(12)]
    public void Every_plan_and_term_sums_to_the_rent_for_the_term_with_contiguous_periods(int plan)
    {
        foreach (var (start, end, rent) in new[] { (D(2026, 1, 31), D(2027, 1, 30), 123_457.89m), (D(2026, 3, 15), D(2027, 9, 14), 77_777.77m), (D(2028, 2, 10), D(2029, 2, 9), 50_000m), (D(2026, 5, 1), D(2026, 7, 20), 9_999.99m) })
        {
            var s = RentSchedule.Generate(start, end, rent, plan);
            Assert.Equal(start, s[0].PeriodFrom);
            Assert.Equal(end, s[^1].PeriodTo);
            for (var i = 1; i < s.Count; i++) Assert.Equal(s[i - 1].PeriodTo.AddDays(1), s[i].PeriodFrom); // no gaps, no overlaps
            Assert.All(s, i => Assert.True(i.Amount >= 0 && i.DueDate == i.PeriodFrom));
            Assert.Equal(Math.Round(RentSchedule.TermRent(s), 2), RentSchedule.TermRent(s)); // 2 dp
        }
    }

    [Fact]
    public void A_short_final_period_is_prorated_by_actual_days_over_365()
    {
        // Jan 1 - Jun 15 quarterly: Q1 is full; Apr 1 - Jun 15 is 76 of 365 days.
        var s = RentSchedule.Generate(D(2026, 1, 1), D(2026, 6, 15), 100_000m, 4);
        Assert.Equal(2, s.Count);
        Assert.Equal(25_000m, s[0].Amount);
        Assert.Equal(Math.Round(100_000m * 76 / 365m, 2), s[1].Amount);
        Assert.Equal(D(2026, 6, 15), s[1].PeriodTo);
    }

    [Fact]
    public void A_final_period_covering_29_february_uses_366_days()
    {
        var s = RentSchedule.Generate(D(2028, 1, 1), D(2028, 3, 10), 366_000m, 1);
        Assert.Single(s);
        Assert.Equal(70_000m, s[0].Amount); // 70 days / 366 x 366,000 (29 Feb 2028 is inside the period)

        var before = RentSchedule.Generate(D(2028, 1, 1), D(2028, 2, 15), 365_000m, 1);
        Assert.Equal(46_000m, before[0].Amount); // 46 days / 365: the period ends before 29 Feb
    }

    [Fact]
    public void Month_end_start_dates_do_not_drift()
    {
        var s = RentSchedule.Generate(D(2026, 1, 31), D(2027, 1, 30), 120_000m, 12);
        Assert.Equal(12, s.Count);
        Assert.Equal(D(2026, 2, 28), s[1].PeriodFrom);
        Assert.Equal(D(2026, 3, 31), s[2].PeriodFrom); // anchored on the start date, not on the clamped previous month
    }

    [Fact]
    public void Grace_days_move_the_first_due_date_and_reduce_the_rent()
    {
        var plain = RentSchedule.Generate(D(2026, 1, 1), D(2026, 12, 31), 120_000m, 12);
        var grace = RentSchedule.Generate(D(2026, 1, 1), D(2026, 12, 31), 120_000m, 12, graceDays: 30);
        Assert.Equal(D(2026, 1, 31), grace[0].DueDate);
        Assert.True(RentSchedule.TermRent(grace) < RentSchedule.TermRent(plain));
        Assert.Equal(D(2026, 12, 31), grace[^1].PeriodTo);
    }

    [Fact]
    public void Invalid_inputs_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RentSchedule.Generate(D(2026, 1, 1), D(2026, 12, 31), 100m, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => RentSchedule.Generate(D(2026, 1, 1), D(2026, 12, 31), 0m, 4));
        Assert.Throws<ArgumentException>(() => RentSchedule.Generate(D(2026, 12, 31), D(2026, 1, 1), 100m, 4));
        Assert.Throws<ArgumentException>(() => RentSchedule.Generate(D(2026, 1, 1), D(2026, 1, 10), 100m, 4, graceDays: 30));
    }
}

public class UnitLifecycleTests
{
    private static readonly DateOnly Today = new(2026, 10, 22);

    private static Unit NewUnit() => Unit.Create(Today.AddDays(-10));

    [Fact]
    public void A_vacant_unit_can_be_reserved_released_and_let()
    {
        var u = NewUnit();
        u.Reserve(Today.AddDays(7));
        Assert.Equal(UnitStatus.Reserved, u.Status);
        u.ReleaseReservation(Today);
        Assert.Equal(UnitStatus.Vacant, u.Status);
        u.Occupy();
        Assert.Equal(UnitStatus.Occupied, u.Status);
        Assert.Null(u.VacantSince);
    }

    [Fact]
    public void Only_leases_can_occupy_and_only_vacant_or_reserved_units_can_be_let()
    {
        var u = NewUnit();
        u.Occupy();
        Assert.Throws<DomainException>(() => u.Occupy());
        Assert.Throws<DomainException>(() => u.Reserve(null));
        Assert.Throws<DomainException>(() => u.MakeUnavailable("renovation"));
    }

    [Fact]
    public void Vacating_records_the_vacancy_date_or_parks_the_unit_when_repairs_are_pending()
    {
        var u = NewUnit(); u.Occupy();
        u.Vacate(Today);
        Assert.Equal((UnitStatus.Vacant, Today), (u.Status, u.VacantSince));

        var v = NewUnit(); v.Occupy();
        v.Vacate(Today, repairsPending: true);
        Assert.Equal(UnitStatus.Unavailable, v.Status);
        Assert.StartsWith("Repairs pending", v.UnavailableReason);
        v.MakeAvailable(Today);
        Assert.Equal(UnitStatus.Vacant, v.Status);
    }

    [Fact]
    public void Making_a_unit_unavailable_needs_a_reason_and_vacating_a_vacant_unit_is_rejected()
    {
        var u = NewUnit();
        Assert.Throws<DomainException>(() => u.MakeUnavailable(" "));
        Assert.Throws<DomainException>(() => u.Vacate(Today));
        Assert.Throws<DomainException>(() => u.MakeAvailable(Today));
        u.MakeUnavailable("Owner use");
        Assert.Equal("Owner use", u.UnavailableReason);
    }
}

public class LeaseLifecycleTests
{
    private static readonly DateOnly Today = new(2026, 10, 22);

    private static Lease NewLease() => new() { StartDate = new(2026, 1, 1), EndDate = new(2026, 12, 31), AnnualRent = 100_000m, PaymentFrequency = 4 };

    [Fact]
    public void A_draft_activates_once()
    {
        var l = NewLease();
        l.Activate(DateTime.UtcNow, Guid.NewGuid());
        Assert.Equal(LeaseStatus.Active, l.Status);
        Assert.True(l.IsOccupying);
        Assert.Throws<DomainException>(() => l.Activate(DateTime.UtcNow, null));
    }

    [Fact]
    public void Only_a_draft_can_be_cancelled()
    {
        var draft = NewLease(); draft.Cancel();
        Assert.Equal(LeaseStatus.Cancelled, draft.Status);
        var active = NewLease(); active.Activate(DateTime.UtcNow, null);
        Assert.Throws<DomainException>(() => active.Cancel());
    }

    [Fact]
    public void Termination_needs_an_active_lease_a_date_inside_the_term_and_a_reason()
    {
        Assert.Throws<DomainException>(() => NewLease().Terminate(new(2026, 6, 30), "x")); // draft
        var l = NewLease(); l.Activate(DateTime.UtcNow, null);
        Assert.Throws<DomainException>(() => l.Terminate(new(2027, 1, 1), "after the end"));
        Assert.Throws<DomainException>(() => l.Terminate(new(2025, 12, 31), "before the start"));
        Assert.Throws<DomainException>(() => l.Terminate(new(2026, 6, 30), " "));
        l.Terminate(new(2026, 6, 30), "Tenant relocation");
        Assert.Equal((LeaseStatus.Terminated, new DateOnly(2026, 6, 30)), (l.Status, l.TerminationDate!.Value));
    }

    [Fact]
    public void Expiry_ends_the_lease_or_holds_it_over_while_the_tenant_stays()
    {
        var ended = NewLease(); ended.Activate(DateTime.UtcNow, null);
        Assert.Equal(LeaseStatus.Ended, ended.Expire(Today.AddYears(1)));

        var held = NewLease(); held.Activate(DateTime.UtcNow, null); held.HoldingOverFlag = true;
        Assert.Equal(LeaseStatus.HoldingOver, held.Expire(Today.AddYears(1)));
        Assert.True(held.IsOccupying);
        held.ConfirmMoveOut();
        Assert.Equal(LeaseStatus.Ended, held.Status);
    }

    [Fact]
    public void A_lease_cannot_expire_before_its_end_date_and_only_active_leases_expire()
    {
        var l = NewLease(); l.Activate(DateTime.UtcNow, null);
        Assert.Throws<DomainException>(() => l.Expire(new(2026, 12, 31))); // end date itself is still inside the term
        Assert.Throws<DomainException>(() => NewLease().Expire(Today.AddYears(1)));
    }

    [Fact]
    public void Renewal_marks_an_occupying_lease_as_renewed()
    {
        var l = NewLease(); l.Activate(DateTime.UtcNow, null);
        l.MarkRenewed();
        Assert.Equal(LeaseStatus.Renewed, l.Status);
        Assert.False(l.IsOccupying);
        Assert.Throws<DomainException>(() => l.MarkRenewed());
    }
}

public class WorkOrderAndDealTests
{
    private static readonly DateOnly Today = new(2026, 10, 22);

    [Fact]
    public void Work_orders_follow_their_lifecycle()
    {
        var w = new WorkOrder { Technician = "Ali" };
        w.MoveTo(WorkOrderStatus.Assigned, Today);
        w.MoveTo(WorkOrderStatus.InProgress, Today);
        w.MoveTo(WorkOrderStatus.OnHold, Today);
        w.MoveTo(WorkOrderStatus.InProgress, Today);
        w.MoveTo(WorkOrderStatus.Completed, Today, "Replaced the compressor");
        Assert.Equal(Today, w.CompletedOn);
        Assert.False(w.IsOpen);
        w.MoveTo(WorkOrderStatus.Closed, Today);
        Assert.Throws<DomainException>(() => w.MoveTo(WorkOrderStatus.InProgress, Today)); // closed is final
    }

    [Fact]
    public void Assigning_needs_a_vendor_or_technician_and_cancelled_orders_are_final()
    {
        var w = new WorkOrder();
        Assert.Throws<DomainException>(() => w.MoveTo(WorkOrderStatus.Assigned, Today));
        Assert.Throws<DomainException>(() => w.MoveTo(WorkOrderStatus.Closed, Today)); // New cannot jump to Closed
        w.MoveTo(WorkOrderStatus.Cancelled, Today);
        Assert.Throws<DomainException>(() => w.MoveTo(WorkOrderStatus.New, Today));
    }

    [Fact]
    public void Reopening_a_completed_order_clears_the_completion_date()
    {
        var w = new WorkOrder { Technician = "Ali" };
        w.MoveTo(WorkOrderStatus.InProgress, Today);
        w.MoveTo(WorkOrderStatus.Completed, Today);
        w.MoveTo(WorkOrderStatus.InProgress, Today);
        Assert.Null(w.CompletedOn);
    }

    [Fact]
    public void Commission_is_value_times_rate_rounded_to_two_decimals()
    {
        var d = new BrokerageDeal { DealValue = 1_234_567.89m, CommissionRatePercent = 2m };
        d.ComputeCommission();
        Assert.Equal(24_691.36m, d.GrossCommission);
    }

    [Fact]
    public void Deals_move_through_the_pipeline_and_can_be_lost_with_a_reason()
    {
        var d = new BrokerageDeal();
        d.MoveTo(DealStage.Viewing); d.MoveTo(DealStage.Negotiation); d.MoveTo(DealStage.Agreed);
        Assert.Equal(DealStage.Agreed, d.Stage);
        Assert.Throws<DomainException>(() => d.MoveTo(DealStage.Invoiced)); // needs Finance
        Assert.Throws<DomainException>(() => d.MoveTo(DealStage.Lost, " "));
        d.MoveTo(DealStage.Lost, "Client chose another agency");
        Assert.Equal("Client chose another agency", d.LostReason);
        Assert.Throws<DomainException>(() => d.MoveTo(DealStage.Lead)); // lost is final
    }
}
