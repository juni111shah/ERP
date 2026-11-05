using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Features;

namespace ERP.UnitTests;

public class ProjectsWave2Tests
{
    private static readonly DateOnly Start = new(2026, 6, 1);

    [Fact]
    public void A_task_takes_the_days_between_its_dates_or_its_estimate_over_eight_hours_or_one_day()
    {
        Assert.Equal(5, ScheduleMath.DurationOf(new(Guid.NewGuid(), Start, Start.AddDays(4), 0, [])));
        Assert.Equal(3, ScheduleMath.DurationOf(new(Guid.NewGuid(), null, null, 17m, [])));
        Assert.Equal(1, ScheduleMath.DurationOf(new(Guid.NewGuid(), null, null, 0m, [])));
    }

    [Fact]
    public void The_critical_path_is_the_longest_chain_and_a_side_task_has_slack()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        var slots = ScheduleMath.Compute([new(a, null, null, 16m, []), new(b, null, null, 24m, [a]), new(c, null, null, 8m, [])], Start);
        Assert.Equal(Start, slots[a].Start);
        Assert.Equal(Start.AddDays(2), slots[b].Start);          // after the two days of A
        Assert.Equal(Start.AddDays(4), slots[b].End);
        Assert.True(slots[a].Critical && slots[b].Critical);
        Assert.False(slots[c].Critical);
        Assert.Equal(4, slots[c].Slack);                         // one day of work in a five-day project
    }

    [Fact]
    public void An_explicit_start_date_holds_a_task_back()
    {
        var a = Guid.NewGuid();
        var slots = ScheduleMath.Compute([new(a, Start.AddDays(10), null, 8m, [])], Start);
        Assert.Equal(Start.AddDays(10), slots[a].Start);
    }

    [Fact]
    public void A_dependency_that_closes_a_loop_is_found()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        var edges = new Dictionary<Guid, List<Guid>> { [b] = [a], [c] = [b] };   // c waits for b, b waits for a
        Assert.True(TaskGraph.WouldLoop(a, [c], edges));                          // a waiting for c would close the loop
        Assert.False(TaskGraph.WouldLoop(c, [a], edges));
    }

    [Fact]
    public void The_most_specific_rate_wins()
    {
        var project = Guid.NewGuid(); var user = Guid.NewGuid();
        var rows = new List<ProjectRate>
        {
            new() { ProjectId = null, BillRate = 10 },
            new() { ProjectId = project, BillRate = 20 },
            new() { ProjectId = project, Role = "Engineer", BillRate = 30 },
            new() { ProjectId = null, UserId = user, BillRate = 40 },
            new() { ProjectId = project, UserId = user, BillRate = 50 },
        };
        Assert.Equal(50, RateResolver.Pick(rows, project, user, "Engineer")!.BillRate);
        Assert.Equal(40, RateResolver.Pick(rows.Where(r => r.BillRate != 50).ToList(), project, user, null)!.BillRate);
        Assert.Equal(30, RateResolver.Pick(rows, project, Guid.NewGuid(), "engineer")!.BillRate);
        Assert.Equal(20, RateResolver.Pick(rows, project, Guid.NewGuid(), null)!.BillRate);
        Assert.Equal(10, RateResolver.Pick(rows, Guid.NewGuid(), null, null)!.BillRate);
        Assert.Null(RateResolver.Pick([], project, null, null));
    }

    [Fact]
    public void Approved_change_orders_move_the_budget_and_drafts_do_not()
    {
        var changes = new[]
        {
            new ProjectChangeOrder { AmountChange = 500, HoursChange = 10, Status = ChangeOrderStatus.Approved },
            new ProjectChangeOrder { AmountChange = 900, Status = ChangeOrderStatus.Draft },
            new ProjectChangeOrder { AmountChange = -100, Status = ChangeOrderStatus.Approved },
            new ProjectChangeOrder { AmountChange = 777, Status = ChangeOrderStatus.Rejected },
        };
        Assert.Equal(1400m, ProjectMath.EffectiveBudget(1000m, changes));
        Assert.Equal(110m, ProjectMath.EffectiveBudgetHours(100m, changes));
    }

    [Fact]
    public void Percent_complete_weights_tasks_by_their_estimate_and_ignores_cancelled_ones()
    {
        var tasks = new[]
        {
            new ProjectTask { Status = ProjectTaskStatus.Done, EstimatedHours = 30 },
            new ProjectTask { Status = ProjectTaskStatus.InProgress, EstimatedHours = 70 },
            new ProjectTask { Status = ProjectTaskStatus.Cancelled, EstimatedHours = 500 },
        };
        Assert.Equal(30m, ProjectMath.PercentComplete(tasks));
        Assert.Equal(0m, ProjectMath.PercentComplete([]));
    }

    [Fact]
    public void Health_goes_from_on_track_to_at_risk_to_off_track_with_spend_and_late_work()
    {
        var today = new DateOnly(2026, 6, 15);
        var p = new Project { Status = ProjectStatus.Active, EndDate = today.AddDays(30) };
        Assert.Equal("onTrack", ProjectMath.Health(p, 1000m, 500m, 0, 3, today));
        Assert.Equal("atRisk", ProjectMath.Health(p, 1000m, 900m, 0, 3, today));
        Assert.Equal("atRisk", ProjectMath.Health(p, 1000m, 100m, 2, 3, today));
        Assert.Equal("offTrack", ProjectMath.Health(p, 1000m, 1200m, 0, 3, today));
        Assert.Equal("closed", ProjectMath.Health(new Project { Status = ProjectStatus.Completed }, 1000m, 5000m, 0, 0, today));
    }

    [Fact]
    public void Report_maths_count_weekdays_ageing_buckets_forecast_and_allocated_hours()
    {
        Assert.Equal(5, ReportMath.WeekdaysBetween(new(2026, 6, 1), new(2026, 6, 7)));   // Monday to Sunday
        Assert.Equal(0, ReportMath.Bucket(new(2026, 6, 1), new(2026, 6, 20)));
        Assert.Equal(1, ReportMath.Bucket(new(2026, 5, 1), new(2026, 6, 20)));
        Assert.Equal(3, ReportMath.Bucket(new(2026, 1, 1), new(2026, 6, 20)));
        Assert.Equal(200m, ReportMath.Forecast(100m, 50m));
        Assert.Equal(100m, ReportMath.Forecast(100m, 0m));
        Assert.Equal(20m, ReportMath.AllocatedHours(new(2026, 6, 1), new(2026, 6, 7), new(2026, 6, 1), new(2026, 6, 30), 20m));
        Assert.Equal(0m, ReportMath.AllocatedHours(new(2026, 6, 1), new(2026, 6, 7), new(2026, 7, 1), new(2026, 7, 30), 20m));
        Assert.Equal(33.3m, ReportMath.Percent(1m, 3m));
    }

    [Fact]
    public void Invoice_lines_group_by_person_task_or_project_and_expenses_carry_their_markup()
    {
        var project = new Project { Number = "PRJ-1", Name = "Site" };
        var taskId = Guid.NewGuid();
        var time = new List<ProjectTimesheet>
        {
            new() { WorkerName = "Ann", Hours = 2, HourlyRate = 50, TotalAmount = 100, TaskId = taskId },
            new() { WorkerName = "Ann", Hours = 1, HourlyRate = 50, TotalAmount = 50, TaskId = taskId },
            new() { WorkerName = "Bob", Hours = 4, HourlyRate = 25, TotalAmount = 100 },
        };
        var expense = new List<ProjectExpense> { new() { Category = "Travel", Description = "Taxi", Amount = 40, MarkupPercent = 10 } };
        var titles = new Dictionary<Guid, string> { [taskId] = "Survey" };
        Assert.Equal(3, BillingLines.Build(project, time, [], [], titles, "detailed", null, null).Count);
        var byPerson = BillingLines.Build(project, time, [], expense, titles, "person", null, null);
        Assert.Equal(3, byPerson.Count);                       // Ann, Bob, the expense
        Assert.Equal(150m, byPerson[0].Price);
        Assert.Equal(44m, byPerson[2].Price);
        var byTask = BillingLines.Build(project, time, [], [], titles, "task", null, null);
        Assert.Contains(byTask, l => l.Description.Contains("Survey") && l.Price == 150m);
        Assert.Single(BillingLines.Build(project, time, [], [], titles, "project", null, null));
        Assert.Equal(25m, BillingLines.Discount(250m, 10m));
    }
}
