using ERP.Application.Platform;
using ERP.Domain.Platform;

namespace ERP.UnitTests;

public class SubscriptionRulesTests
{
    private static readonly DateOnly Today = new(2026, 6, 15);
    private static readonly PlatformSetting Settings = new() { GraceDays = 7, SuspendAfterDays = 30 };
    private static readonly SubscriptionPlan Plan = new() { MonthlyPrice = 100, AnnualPrice = 1000, TrialDays = 14 };

    private static SubscriptionAccessLevel Access(WorkspaceSubscription s) => SubscriptionRules.AccessOf(s, Settings, Today);

    [Theory]
    [InlineData(SubscriptionStatus.Trialing, SubscriptionAccessLevel.Full)]
    [InlineData(SubscriptionStatus.Active, SubscriptionAccessLevel.Full)]
    [InlineData(SubscriptionStatus.Expired, SubscriptionAccessLevel.ReadOnly)]
    [InlineData(SubscriptionStatus.Suspended, SubscriptionAccessLevel.Blocked)]
    [InlineData(SubscriptionStatus.Cancelled, SubscriptionAccessLevel.Blocked)]
    public void Each_status_gives_its_access(SubscriptionStatus status, SubscriptionAccessLevel expected)
        => Assert.Equal(expected, Access(new WorkspaceSubscription { Status = status }));

    [Fact]
    public void A_trial_whose_date_has_passed_is_view_only_even_before_the_daily_run()
    {
        Assert.Equal(SubscriptionAccessLevel.Full, Access(new WorkspaceSubscription { Status = SubscriptionStatus.Trialing, TrialEndsOn = Today }));
        Assert.Equal(SubscriptionAccessLevel.ReadOnly, Access(new WorkspaceSubscription { Status = SubscriptionStatus.Trialing, TrialEndsOn = Today.AddDays(-1) }));
    }

    [Fact]
    public void Past_due_keeps_full_use_through_the_last_grace_day()
    {
        Assert.Equal(SubscriptionAccessLevel.Full, Access(new WorkspaceSubscription { Status = SubscriptionStatus.PastDue, LapsedOn = Today.AddDays(-7) }));
        Assert.Equal(SubscriptionAccessLevel.ReadOnly, Access(new WorkspaceSubscription { Status = SubscriptionStatus.PastDue, LapsedOn = Today.AddDays(-8) }));
    }

    [Fact]
    public void Free_of_charge_never_turns_view_only_but_can_still_be_suspended()
    {
        Assert.Equal(SubscriptionAccessLevel.Full, Access(new WorkspaceSubscription { Status = SubscriptionStatus.Expired, IsComplimentary = true }));
        Assert.Equal(SubscriptionAccessLevel.Blocked, Access(new WorkspaceSubscription { Status = SubscriptionStatus.Suspended, IsComplimentary = true }));
    }

    [Theory]
    [InlineData("2026-01-15", BillingCycle.Monthly, "2026-02-14")]
    [InlineData("2026-12-01", BillingCycle.Monthly, "2026-12-31")]
    [InlineData("2026-01-31", BillingCycle.Monthly, "2026-02-27")]
    [InlineData("2026-03-10", BillingCycle.Annual, "2027-03-09")]
    [InlineData("2028-02-29", BillingCycle.Annual, "2029-02-27")]
    public void A_period_ends_the_day_before_the_next_one_starts(string start, BillingCycle cycle, string end)
        => Assert.Equal(DateOnly.Parse(end), SubscriptionRules.PeriodEnd(DateOnly.Parse(start), cycle));

    [Fact]
    public void A_custom_price_replaces_the_list_price_and_free_of_charge_beats_both()
    {
        Assert.Equal(100, SubscriptionRules.CyclePrice(new WorkspaceSubscription { Cycle = BillingCycle.Monthly }, Plan));
        Assert.Equal(1000, SubscriptionRules.CyclePrice(new WorkspaceSubscription { Cycle = BillingCycle.Annual }, Plan));
        Assert.Equal(80, SubscriptionRules.CyclePrice(new WorkspaceSubscription { Cycle = BillingCycle.Monthly, CustomPrice = 80 }, Plan));
        Assert.Equal(0, SubscriptionRules.CyclePrice(new WorkspaceSubscription { Cycle = BillingCycle.Monthly, CustomPrice = 80, IsComplimentary = true }, Plan));
        Assert.Equal(83.33m, SubscriptionRules.MonthlyValue(new WorkspaceSubscription { Cycle = BillingCycle.Annual }, Plan));
    }

    [Fact]
    public void Starting_follows_the_chosen_mode()
    {
        var trial = SubscriptionRules.Start(Plan, BillingCycle.Monthly, Today, ClientStartMode.Trial);
        Assert.Equal(SubscriptionStatus.Trialing, trial.Status);
        Assert.Equal(Today.AddDays(14), trial.TrialEndsOn);

        Assert.Equal(Today.AddDays(30), SubscriptionRules.Start(Plan, BillingCycle.Monthly, Today, ClientStartMode.Trial, 30).TrialEndsOn);

        // A plan with no trial starts running straight away.
        var noTrial = SubscriptionRules.Start(new SubscriptionPlan { TrialDays = 0 }, BillingCycle.Monthly, Today, ClientStartMode.Trial);
        Assert.Equal(SubscriptionStatus.Active, noTrial.Status);
        Assert.Equal(Today, noTrial.CurrentPeriodStart);
        Assert.NotNull(noTrial.CurrentPeriodEnd);

        var free = SubscriptionRules.Start(Plan, BillingCycle.Monthly, Today, ClientStartMode.Complimentary);
        Assert.True(free.IsComplimentary);
        Assert.Equal(SubscriptionStatus.Active, free.Status);
        Assert.Null(free.CurrentPeriodEnd);
    }

    private static SubscriptionInvoice Invoice(DateOnly? start = null, DateOnly? end = null) => new() { PeriodStart = start, PeriodEnd = end };

    [Fact]
    public void Paying_brings_a_lapsed_client_back_and_moves_the_period()
    {
        var s = new WorkspaceSubscription { Status = SubscriptionStatus.PastDue, LapsedOn = Today.AddDays(-3), CurrentPeriodEnd = Today.AddDays(-4) };
        SubscriptionRules.ApplyPayment(s, Invoice(Today.AddDays(-3), Today.AddDays(27)), false, Today);
        Assert.Equal(SubscriptionStatus.Active, s.Status);
        Assert.Null(s.LapsedOn);
        Assert.Equal(Today.AddDays(27), s.CurrentPeriodEnd);
    }

    [Fact]
    public void Another_overdue_invoice_keeps_the_client_past_due()
    {
        var s = new WorkspaceSubscription { Status = SubscriptionStatus.PastDue, LapsedOn = Today.AddDays(-3), CurrentPeriodEnd = Today.AddDays(-4) };
        SubscriptionRules.ApplyPayment(s, Invoice(Today.AddDays(-3), Today.AddDays(27)), true, Today);
        Assert.Equal(SubscriptionStatus.PastDue, s.Status);
        Assert.Equal(Today.AddDays(27), s.CurrentPeriodEnd);
    }

    [Theory]
    [InlineData(SubscriptionStatus.Cancelled, false)]
    [InlineData(SubscriptionStatus.Suspended, true)]
    public void A_payment_does_not_undo_the_founders_decision(SubscriptionStatus status, bool byFounder)
    {
        var s = new WorkspaceSubscription { Status = status, SuspendedByFounder = byFounder };
        SubscriptionRules.ApplyPayment(s, Invoice(Today, Today.AddDays(30)), false, Today);
        Assert.Equal(status, s.Status);
    }

    [Fact]
    public void A_one_off_invoice_does_not_end_a_trial()
    {
        var s = new WorkspaceSubscription { Status = SubscriptionStatus.Trialing, TrialEndsOn = Today.AddDays(5) };
        SubscriptionRules.ApplyPayment(s, Invoice(), false, Today);
        Assert.Equal(SubscriptionStatus.Trialing, s.Status);
    }

    [Fact]
    public void A_payment_for_an_older_period_never_shortens_the_current_one()
    {
        var s = new WorkspaceSubscription { Status = SubscriptionStatus.Active, CurrentPeriodStart = Today, CurrentPeriodEnd = Today.AddDays(30) };
        SubscriptionRules.ApplyPayment(s, Invoice(Today.AddDays(-30), Today.AddDays(-1)), false, Today);
        Assert.Equal(Today.AddDays(30), s.CurrentPeriodEnd);
    }

    [Fact]
    public void Reactivating_returns_to_the_state_the_dates_support()
    {
        WorkspaceSubscription Suspended(Action<WorkspaceSubscription> set)
        {
            var s = new WorkspaceSubscription { Status = SubscriptionStatus.Suspended, SuspendedByFounder = true, SuspendedReason = "x" };
            set(s);
            return s;
        }
        var paid = Suspended(s => s.CurrentPeriodEnd = Today.AddDays(5));
        SubscriptionRules.Reactivate(paid, false, Today);
        Assert.Equal(SubscriptionStatus.Active, paid.Status);
        Assert.False(paid.SuspendedByFounder);
        Assert.Null(paid.SuspendedReason);

        var owing = Suspended(s => s.CurrentPeriodEnd = Today.AddDays(5));
        SubscriptionRules.Reactivate(owing, true, Today);
        Assert.Equal(SubscriptionStatus.PastDue, owing.Status);
        Assert.Equal(Today, owing.LapsedOn);

        var trial = Suspended(s => s.TrialEndsOn = Today.AddDays(3));
        SubscriptionRules.Reactivate(trial, false, Today);
        Assert.Equal(SubscriptionStatus.Trialing, trial.Status);

        var lapsed = Suspended(s => s.CurrentPeriodEnd = Today.AddDays(-5));
        SubscriptionRules.Reactivate(lapsed, false, Today);
        Assert.Equal(SubscriptionStatus.Expired, lapsed.Status);

        var free = Suspended(s => s.IsComplimentary = true);
        SubscriptionRules.Reactivate(free, true, Today);
        Assert.Equal(SubscriptionStatus.Active, free.Status);
    }
}
