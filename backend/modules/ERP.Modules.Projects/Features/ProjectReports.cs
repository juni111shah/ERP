using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.Projects.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Features;

public sealed record ProfitabilityRow(Guid ProjectId, string Number, string Name, string? CustomerName, ProjectStatus Status, ProjectBillingType BillingType,
    decimal Hours, decimal BillableHours, decimal Billed, decimal Unbilled, decimal LabourCost, decimal ExpenseCost, decimal Cost, decimal Margin, decimal MarginPercent);
public sealed record UtilisationRow(string Person, Guid? UserId, decimal LoggedHours, decimal BillableHours, decimal CapacityHours, decimal AllocatedHours, decimal UtilisationPercent, decimal BillablePercent);
public sealed record BudgetRow(Guid ProjectId, string Number, string Name, ProjectStatus Status, ProjectBillingType BillingType, decimal Budget, decimal ApprovedChanges, decimal Cost, decimal UsedPercent,
    decimal BudgetHours, decimal LoggedHours, decimal PercentComplete, decimal ForecastCost, decimal ForecastVariance, decimal EarnedRevenue, decimal Billed, decimal WorkInProgress, string Health,
    decimal PlannedValue = 0m, decimal EarnedValue = 0m, decimal? CostIndex = null, decimal? ScheduleIndex = null);
public sealed record AgeingRow(Guid ProjectId, string Number, string Name, decimal Current, decimal Days31To60, decimal Days61To90, decimal Over90, decimal Total);
public sealed record HoursRow(string Group, decimal Hours, decimal BillableHours, decimal Amount, decimal Cost);

public sealed record GetProfitabilityReport(DateOnly? From, DateOnly? To, Guid? ProjectId) : IQuery<IReadOnlyList<ProfitabilityRow>>, IRequirePermission { public string RequiredPermission => P.ReportsRead; }
public sealed record GetUtilisationReport(DateOnly From, DateOnly To) : IQuery<IReadOnlyList<UtilisationRow>>, IRequirePermission { public string RequiredPermission => P.ReportsRead; }
public sealed record GetBudgetReport() : IQuery<IReadOnlyList<BudgetRow>>, IRequirePermission { public string RequiredPermission => P.ReportsRead; }
public sealed record GetUnbilledAgeing(DateOnly? AsOf) : IQuery<IReadOnlyList<AgeingRow>>, IRequirePermission { public string RequiredPermission => P.ReportsRead; }
public sealed record GetHoursReport(DateOnly From, DateOnly To, string GroupBy, Guid? ProjectId) : IQuery<IReadOnlyList<HoursRow>>, IRequirePermission { public string RequiredPermission => P.ReportsRead; }

public sealed class GetUtilisationReportValidator : AbstractValidator<GetUtilisationReport>
{
    public GetUtilisationReportValidator()
    {
        RuleFor(x => x).Must(x => x.To >= x.From).WithName("to").WithMessage("The end date cannot be before the start date.");
        RuleFor(x => x).Must(x => x.To.DayNumber - x.From.DayNumber <= 400).WithName("to").WithMessage("Choose a period of at most 400 days.");
    }
}
public sealed class GetHoursReportValidator : AbstractValidator<GetHoursReport>
{
    public GetHoursReportValidator()
    {
        RuleFor(x => x.GroupBy).Must(g => g is "person" or "project" or "task").WithMessage("Group by person, project or task.");
        RuleFor(x => x).Must(x => x.To >= x.From).WithName("to").WithMessage("The end date cannot be before the start date.");
    }
}

/// <summary>Report arithmetic; no I/O.</summary>
public static class ReportMath
{
    public static decimal Percent(decimal part, decimal whole) => whole == 0 ? 0m : Math.Round(part / whole * 100m, 1);

    private static readonly DayOfWeek[] MondayToFriday = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    /// <summary>The working days from the setting "Sun,Mon,Tue"; Monday to Friday when it is empty or unreadable.</summary>
    public static IReadOnlyCollection<DayOfWeek> ParseWorkWeek(string? text)
    {
        var days = (text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => Enum.GetValues<DayOfWeek>().Where(d => d.ToString().StartsWith(t.Length >= 3 ? t[..3] : "?", StringComparison.OrdinalIgnoreCase)).Cast<DayOfWeek?>().FirstOrDefault())
            .Where(d => d != null).Select(d => d!.Value).Distinct().ToList();
        return days.Count == 0 ? MondayToFriday : days;
    }

    public static int WorkdaysBetween(DateOnly from, DateOnly to, IReadOnlyCollection<DayOfWeek> week)
    {
        var n = 0;
        for (var d = from; d <= to; d = d.AddDays(1)) if (week.Contains(d.DayOfWeek)) n++;
        return n;
    }

    /// <summary>Share of the planned time that has passed, 0 to 1; a project without both dates has no plan to compare with.</summary>
    public static decimal? PlannedShare(DateOnly? start, DateOnly? end, DateOnly today)
    {
        if (start is not { } s || end is not { } e || e < s) return null;
        if (today <= s) return 0m;
        if (today >= e) return 1m;
        return (decimal)(today.DayNumber - s.DayNumber) / (e.DayNumber - s.DayNumber);
    }

    /// <summary>Working days (Monday to Friday) between two dates, inclusive; the unit of "capacity".</summary>
    public static int WeekdaysBetween(DateOnly from, DateOnly to)
    {
        var n = 0;
        for (var d = from; d <= to; d = d.AddDays(1)) if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) n++;
        return n;
    }

    /// <summary>Hours planned in the window for an allocation of HoursPerWeek between its dates (spread over weekdays).</summary>
    public static decimal AllocatedHours(DateOnly from, DateOnly to, DateOnly allocFrom, DateOnly allocTo, decimal hoursPerWeek)
    {
        var a = allocFrom > from ? allocFrom : from; var b = allocTo < to ? allocTo : to;
        return b < a ? 0m : Math.Round(WeekdaysBetween(a, b) * hoursPerWeek / 5m, 2);
    }

    /// <summary>Forecast at completion: spend so far divided by the share done; with nothing done yet the spend itself.</summary>
    public static decimal Forecast(decimal cost, decimal percentComplete) => percentComplete <= 0 ? cost : Math.Round(cost / (percentComplete / 100m), 2);

    /// <summary>Ageing bucket 0..3 by days since the date.</summary>
    public static int Bucket(DateOnly date, DateOnly asOf)
    {
        var days = asOf.DayNumber - date.DayNumber;
        return days <= 30 ? 0 : days <= 60 ? 1 : days <= 90 ? 2 : 3;
    }
}

internal sealed class ReportHandlers(
    IReadRepository<Project> projects,
    IReadRepository<ProjectTimesheet> timesheets,
    IReadRepository<ProjectExpense> expenses,
    IReadRepository<ProjectMilestone> milestones,
    IReadRepository<ProjectTask> tasks,
    IReadRepository<ProjectChangeOrder> changeOrders,
    IReadRepository<ProjectProgressBilling> progress,
    IReadRepository<ProjectAllocation> allocations,
    ProjectAccess access,
    ISettingsService settings) :
    IRequestHandler<GetProfitabilityReport, IReadOnlyList<ProfitabilityRow>>,
    IRequestHandler<GetUtilisationReport, IReadOnlyList<UtilisationRow>>,
    IRequestHandler<GetBudgetReport, IReadOnlyList<BudgetRow>>,
    IRequestHandler<GetUnbilledAgeing, IReadOnlyList<AgeingRow>>,
    IRequestHandler<GetHoursReport, IReadOnlyList<HoursRow>>
{
    public async Task<IReadOnlyList<ProfitabilityRow>> Handle(GetProfitabilityReport q, CancellationToken ct)
    {
        var visible = await access.VisibleIdsAsync(ct);
        var list = (await projects.Query(new Specification<Project>(p => !p.IsTemplate && (q.ProjectId == null || p.Id == q.ProjectId))).ToListAsync(ct)).Where(p => visible == null || visible.Contains(p.Id)).ToList();
        var ids = list.Select(p => p.Id).ToList();
        var ts = (await timesheets.Query(new Specification<ProjectTimesheet>(t => ids.Contains(t.ProjectId) && t.ApprovalStatus != TimesheetStatus.Rejected && (q.From == null || t.Date >= q.From) && (q.To == null || t.Date <= q.To))).ToListAsync(ct)).ToLookup(t => t.ProjectId);
        var ex = (await expenses.Query(new Specification<ProjectExpense>(e => ids.Contains(e.ProjectId) && e.Status == ExpenseStatus.Approved && (q.From == null || e.Date >= q.From) && (q.To == null || e.Date <= q.To))).ToListAsync(ct)).ToLookup(e => e.ProjectId);
        // a milestone counts in the period it was reached, like time and expenses count on their own dates
        var ms = (await milestones.Query(new Specification<ProjectMilestone>(m => ids.Contains(m.ProjectId) && (m.Status == MilestoneStatus.Achieved || m.Status == MilestoneStatus.Invoiced))).ToListAsync(ct))
            .Where(m => (m.AchievedDate ?? m.DueDate) is var d && (q.From == null || d >= q.From) && (q.To == null || d <= q.To)).ToLookup(m => m.ProjectId);
        var pr = (await progress.Query(new Specification<ProjectProgressBilling>(x => ids.Contains(x.ProjectId) && !x.Released && (q.From == null || x.Date >= q.From) && (q.To == null || x.Date <= q.To))).ToListAsync(ct)).ToLookup(x => x.ProjectId);
        var rows = new List<ProfitabilityRow>();
        foreach (var p in list.OrderBy(p => p.Number))
        {
            var t = ts[p.Id].ToList(); var e = ex[p.Id].ToList();
            var billed = t.Where(x => x.IsBilled).Sum(x => x.TotalAmount) + e.Where(x => x.IsBilled).Sum(BillingLines.Charge) + ms[p.Id].Where(m => m.Status == MilestoneStatus.Invoiced).Sum(m => m.Amount) + pr[p.Id].Sum(x => x.Amount);
            var unbilled = p.BillingType == ProjectBillingType.NonBillable ? 0m
                : (p.BillingType == ProjectBillingType.FixedFee ? 0m : t.Where(x => x.IsBillable && !x.IsBilled && !x.WrittenOff && x.ApprovalStatus == TimesheetStatus.Approved).Sum(x => x.TotalAmount))
                  + e.Where(x => x.IsBillable && !x.IsBilled && !x.WrittenOff).Sum(BillingLines.Charge) + ms[p.Id].Where(m => m.Status == MilestoneStatus.Achieved).Sum(m => m.Amount);
            var labour = t.Sum(x => x.CostAmount); var expenseCost = e.Sum(x => x.Amount);
            var cost = labour + expenseCost; var margin = billed + unbilled - cost;
            rows.Add(new ProfitabilityRow(p.Id, p.Number, p.Name, p.CustomerName, p.Status, p.BillingType, t.Sum(x => x.Hours), t.Where(x => x.IsBillable && !x.WrittenOff).Sum(x => x.Hours),
                billed, unbilled, labour, expenseCost, cost, margin, ReportMath.Percent(margin, billed + unbilled)));
        }
        return rows;
    }

    public async Task<IReadOnlyList<UtilisationRow>> Handle(GetUtilisationReport q, CancellationToken ct)
    {
        var capacityPerWeek = await settings.GetAsync<decimal?>("projects.weeklyCapacityHours", null, ct) ?? 40m;
        var week = ReportMath.ParseWorkWeek(await settings.GetAsync<string?>("projects.workWeekDays", null, ct));
        var days = ReportMath.WorkdaysBetween(q.From, q.To, week);
        var capacity = Math.Round(days * capacityPerWeek / week.Count, 2);
        var visible = await access.VisibleIdsAsync(ct);
        var time = (await timesheets.Query(new Specification<ProjectTimesheet>(t => t.Date >= q.From && t.Date <= q.To && t.ApprovalStatus != TimesheetStatus.Rejected)).ToListAsync(ct)).Where(t => visible == null || visible.Contains(t.ProjectId)).ToList();
        var plan = (await allocations.Query(new Specification<ProjectAllocation>(a => a.FromDate <= q.To && a.ToDate >= q.From)).ToListAsync(ct)).Where(a => visible == null || visible.Contains(a.ProjectId)).ToList();
        var people = time.Select(t => (Key: t.WorkerUserId?.ToString() ?? "n:" + t.WorkerName.ToLowerInvariant(), Name: t.WorkerName, UserId: t.WorkerUserId))
            .Concat(plan.Select(a => (Key: a.UserId.ToString(), Name: a.UserName, UserId: (Guid?)a.UserId))).GroupBy(x => x.Key).Select(g => g.First()).OrderBy(x => x.Name).ToList();
        var rows = new List<UtilisationRow>();
        foreach (var person in people)
        {
            var mine = time.Where(t => (t.WorkerUserId?.ToString() ?? "n:" + t.WorkerName.ToLowerInvariant()) == person.Key).ToList();
            var logged = mine.Sum(t => t.Hours); var billable = mine.Where(t => t.IsBillable && !t.WrittenOff).Sum(t => t.Hours);
            var planned = plan.Where(a => a.UserId.ToString() == person.Key).Sum(a => ReportMath.AllocatedHours(q.From, q.To, a.FromDate, a.ToDate, a.HoursPerWeek));
            rows.Add(new UtilisationRow(person.Name, person.UserId, logged, billable, capacity, planned, ReportMath.Percent(logged, capacity), ReportMath.Percent(billable, logged)));
        }
        return rows;
    }

    public async Task<IReadOnlyList<BudgetRow>> Handle(GetBudgetReport q, CancellationToken ct)
    {
        var visible = await access.VisibleIdsAsync(ct);
        var list = (await projects.Query(new Specification<Project>(p => !p.IsTemplate && !p.IsArchived && p.Status != ProjectStatus.Cancelled)).ToListAsync(ct)).Where(p => visible == null || visible.Contains(p.Id)).ToList();
        var ids = list.Select(p => p.Id).ToList();
        var ts = (await timesheets.Query(new Specification<ProjectTimesheet>(t => ids.Contains(t.ProjectId) && t.ApprovalStatus != TimesheetStatus.Rejected)).ToListAsync(ct)).ToLookup(t => t.ProjectId);
        var ex = (await expenses.Query(new Specification<ProjectExpense>(e => ids.Contains(e.ProjectId) && e.Status == ExpenseStatus.Approved)).ToListAsync(ct)).ToLookup(e => e.ProjectId);
        var tk = (await tasks.Query(new Specification<ProjectTask>(t => ids.Contains(t.ProjectId))).ToListAsync(ct)).ToLookup(t => t.ProjectId);
        var co = (await changeOrders.Query(new Specification<ProjectChangeOrder>(c => ids.Contains(c.ProjectId))).ToListAsync(ct)).ToLookup(c => c.ProjectId);
        var ms = (await milestones.Query(new Specification<ProjectMilestone>(m => ids.Contains(m.ProjectId) && m.Status == MilestoneStatus.Invoiced)).ToListAsync(ct)).ToLookup(m => m.ProjectId);
        var pr = (await progress.Query(new Specification<ProjectProgressBilling>(x => ids.Contains(x.ProjectId) && !x.Released)).ToListAsync(ct)).ToLookup(x => x.ProjectId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rows = new List<BudgetRow>();
        foreach (var p in list.OrderBy(p => p.Number))
        {
            var budget = ProjectMath.EffectiveBudget(p.BudgetAmount, co[p.Id]);
            var hoursBudget = ProjectMath.EffectiveBudgetHours(p.BudgetHours, co[p.Id]);
            var cost = ts[p.Id].Sum(x => x.CostAmount) + ex[p.Id].Sum(x => x.Amount);
            var logged = ts[p.Id].Sum(x => x.Hours);
            var pct = ProjectMath.PercentComplete(tk[p.Id]);
            var billed = ts[p.Id].Where(x => x.IsBilled).Sum(x => x.TotalAmount) + ex[p.Id].Where(x => x.IsBilled).Sum(BillingLines.Charge) + ms[p.Id].Sum(m => m.Amount) + pr[p.Id].Sum(x => x.Amount);
            var forecast = ReportMath.Forecast(cost, pct);
            var earned = p.BillingType == ProjectBillingType.FixedFee ? Math.Round(budget * pct / 100m, 2) : 0m;
            var wip = p.BillingType == ProjectBillingType.FixedFee ? earned - billed : 0m;
            var tasksOpen = tk[p.Id].Where(t => t.Status is not (ProjectTaskStatus.Done or ProjectTaskStatus.Cancelled)).ToList();
            var overdue = tasksOpen.Count(t => t.DueDate is { } d && d < today);
            // earned value: what the finished share of the work was budgeted to cost, against the spend and against the plan
            var ev = Math.Round(budget * pct / 100m, 2);
            var share = ReportMath.PlannedShare(p.StartDate, p.EndDate, today);
            var pv = share is { } planned ? Math.Round(budget * planned, 2) : 0m;
            rows.Add(new BudgetRow(p.Id, p.Number, p.Name, p.Status, p.BillingType, budget, budget - p.BudgetAmount, cost, ReportMath.Percent(cost, budget), hoursBudget, logged, pct, forecast,
                budget > 0 ? budget - forecast : 0m, earned, billed, wip, ProjectMath.Health(p, budget, cost, overdue, tasksOpen.Count, today),
                pv, ev, cost > 0 && budget > 0 ? Math.Round(ev / cost, 2) : null, pv > 0 ? Math.Round(ev / pv, 2) : null));
        }
        return rows;
    }

    public async Task<IReadOnlyList<AgeingRow>> Handle(GetUnbilledAgeing q, CancellationToken ct)
    {
        var asOf = q.AsOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var visible = await access.VisibleIdsAsync(ct);
        var list = (await projects.Query(new Specification<Project>(p => !p.IsTemplate && p.BillingType != ProjectBillingType.NonBillable)).ToListAsync(ct)).Where(p => visible == null || visible.Contains(p.Id)).ToList();
        var ids = list.Select(p => p.Id).ToList();
        var ts = (await timesheets.Query(new Specification<ProjectTimesheet>(t => ids.Contains(t.ProjectId) && t.IsBillable && !t.IsBilled && !t.WrittenOff && t.ApprovalStatus == TimesheetStatus.Approved)).ToListAsync(ct)).ToLookup(t => t.ProjectId);
        var ex = (await expenses.Query(new Specification<ProjectExpense>(e => ids.Contains(e.ProjectId) && e.IsBillable && !e.IsBilled && !e.WrittenOff && e.Status == ExpenseStatus.Approved)).ToListAsync(ct)).ToLookup(e => e.ProjectId);
        var ms = (await milestones.Query(new Specification<ProjectMilestone>(m => ids.Contains(m.ProjectId) && m.Status == MilestoneStatus.Achieved)).ToListAsync(ct)).ToLookup(m => m.ProjectId);
        var rows = new List<AgeingRow>();
        foreach (var p in list.OrderBy(p => p.Number))
        {
            var b = new decimal[4];
            if (p.BillingType != ProjectBillingType.FixedFee) foreach (var t in ts[p.Id]) b[ReportMath.Bucket(t.Date, asOf)] += t.TotalAmount;
            foreach (var e in ex[p.Id]) b[ReportMath.Bucket(e.Date, asOf)] += BillingLines.Charge(e);
            foreach (var m in ms[p.Id]) b[ReportMath.Bucket(m.AchievedDate ?? m.DueDate, asOf)] += m.Amount;
            var total = b.Sum();
            if (total > 0) rows.Add(new AgeingRow(p.Id, p.Number, p.Name, b[0], b[1], b[2], b[3], total));
        }
        return rows;
    }

    public async Task<IReadOnlyList<HoursRow>> Handle(GetHoursReport q, CancellationToken ct)
    {
        var visible = await access.VisibleIdsAsync(ct);
        var time = (await timesheets.Query(new Specification<ProjectTimesheet>(t => t.Date >= q.From && t.Date <= q.To && t.ApprovalStatus != TimesheetStatus.Rejected && (q.ProjectId == null || t.ProjectId == q.ProjectId))).ToListAsync(ct)).Where(t => visible == null || visible.Contains(t.ProjectId)).ToList();
        Dictionary<Guid, string> names = new();
        if (q.GroupBy == "project") { var pids = time.Select(t => t.ProjectId).Distinct().ToList(); names = await projects.Query(new Specification<Project>(p => pids.Contains(p.Id))).ToDictionaryAsync(p => p.Id, p => p.Number + " " + p.Name, ct); }
        if (q.GroupBy == "task") { var tids = time.Where(t => t.TaskId != null).Select(t => t.TaskId!.Value).Distinct().ToList(); names = await tasks.Query(new Specification<ProjectTask>(t => tids.Contains(t.Id))).ToDictionaryAsync(t => t.Id, t => t.Number + " " + t.Title, ct); }
        string Key(ProjectTimesheet t) => q.GroupBy switch { "project" => names.GetValueOrDefault(t.ProjectId, "?"), "task" => t.TaskId is { } id ? names.GetValueOrDefault(id, "?") : "No task", _ => t.WorkerName };
        return time.GroupBy(Key).OrderBy(g => g.Key)
            .Select(g => new HoursRow(g.Key, g.Sum(t => t.Hours), g.Where(t => t.IsBillable && !t.WrittenOff).Sum(t => t.Hours), g.Sum(t => t.TotalAmount), g.Sum(t => t.CostAmount))).ToList();
    }
}
