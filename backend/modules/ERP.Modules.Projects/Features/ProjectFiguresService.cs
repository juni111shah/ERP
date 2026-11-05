using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Features;

/// <summary>The figures of one project. Every screen reads them from here, so "unbilled" and "cost" mean the same thing everywhere.</summary>
public sealed record ProjectFigureRow(
    decimal LoggedHours, decimal BilledHours, decimal LabourCost, decimal ExpenseCost, decimal BillableValue, decimal Billed, decimal Unbilled,
    decimal Budget, decimal ApprovedChanges, decimal BudgetHours, decimal PercentComplete, string Health, int TotalTasks, int CompletedTasks, int OpenTasks, int OverdueTasks, int TotalMilestones, int AchievedMilestones)
{
    public decimal Cost => LabourCost + ExpenseCost;
}

public sealed class ProjectFigures(
    IReadRepository<ProjectTimesheet> timesheets,
    IReadRepository<ProjectTask> tasks,
    IReadRepository<ProjectMilestone> milestones,
    IReadRepository<ProjectExpense> expenses,
    IReadRepository<ProjectChangeOrder> changeOrders,
    IReadRepository<ProjectProgressBilling> progress)
{
    public async Task<IReadOnlyDictionary<Guid, ProjectFigureRow>> ForAsync(IReadOnlyCollection<Project> list, CancellationToken ct)
    {
        var ids = list.Select(p => p.Id).ToList();
        if (ids.Count == 0) return new Dictionary<Guid, ProjectFigureRow>();
        var ts = (await timesheets.Query(new Specification<ProjectTimesheet>(x => ids.Contains(x.ProjectId))).ToListAsync(ct)).ToLookup(x => x.ProjectId);
        var tk = (await tasks.Query(new Specification<ProjectTask>(x => ids.Contains(x.ProjectId))).ToListAsync(ct)).ToLookup(x => x.ProjectId);
        var ms = (await milestones.Query(new Specification<ProjectMilestone>(x => ids.Contains(x.ProjectId))).ToListAsync(ct)).ToLookup(x => x.ProjectId);
        var ex = (await expenses.Query(new Specification<ProjectExpense>(x => ids.Contains(x.ProjectId))).ToListAsync(ct)).ToLookup(x => x.ProjectId);
        var co = (await changeOrders.Query(new Specification<ProjectChangeOrder>(x => ids.Contains(x.ProjectId))).ToListAsync(ct)).ToLookup(x => x.ProjectId);
        var pr = (await progress.Query(new Specification<ProjectProgressBilling>(x => ids.Contains(x.ProjectId) && !x.Released)).ToListAsync(ct)).ToLookup(x => x.ProjectId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return list.ToDictionary(p => p.Id, p => Compute(p, ts[p.Id].ToList(), tk[p.Id].ToList(), ms[p.Id].ToList(), ex[p.Id].ToList(), co[p.Id].ToList(), pr[p.Id].ToList(), today));
    }

    public async Task<ProjectFigureRow> ForAsync(Project p, CancellationToken ct) => (await ForAsync([p], ct))[p.Id];

    /// <summary>The arithmetic itself; no I/O. Rejected time is not a cost; a fixed-fee project bills milestones and percentages, not hours.</summary>
    public static ProjectFigureRow Compute(Project p, IReadOnlyCollection<ProjectTimesheet> ts, IReadOnlyCollection<ProjectTask> tk, IReadOnlyCollection<ProjectMilestone> ms, IReadOnlyCollection<ProjectExpense> ex, IReadOnlyCollection<ProjectChangeOrder> co, IReadOnlyCollection<ProjectProgressBilling> pr, DateOnly today)
    {
        var labour = ts.Where(x => x.ApprovalStatus != TimesheetStatus.Rejected).Sum(x => x.CostAmount);
        var expense = ex.Where(x => x.Status == ExpenseStatus.Approved).Sum(x => x.Amount);
        var billed = ts.Where(x => x.IsBilled).Sum(x => x.TotalAmount) + ex.Where(x => x.IsBilled).Sum(BillingLines.Charge) + ms.Where(x => x.Status == MilestoneStatus.Invoiced).Sum(x => x.Amount) + pr.Sum(x => x.Amount);
        var unbilled = p.BillingType == ProjectBillingType.NonBillable ? 0m
            : (p.BillingType == ProjectBillingType.FixedFee ? 0m : ts.Where(x => x.IsBillable && !x.IsBilled && !x.WrittenOff && x.ApprovalStatus == TimesheetStatus.Approved).Sum(x => x.TotalAmount))
              + ex.Where(x => x.IsBillable && !x.IsBilled && !x.WrittenOff && x.Status == ExpenseStatus.Approved).Sum(BillingLines.Charge)
              + ms.Where(x => x.Status == MilestoneStatus.Achieved).Sum(x => x.Amount);
        var budget = ProjectMath.EffectiveBudget(p.BudgetAmount, co);
        var open = tk.Count(x => x.Status is not (ProjectTaskStatus.Done or ProjectTaskStatus.Cancelled));
        var overdue = tk.Count(x => x.DueDate is { } d && d < today && x.Status is not (ProjectTaskStatus.Done or ProjectTaskStatus.Cancelled));
        return new ProjectFigureRow(
            ts.Sum(x => x.Hours), ts.Where(x => x.IsBilled).Sum(x => x.Hours), labour, expense, ts.Where(x => x.IsBillable && !x.WrittenOff).Sum(x => x.TotalAmount), billed, unbilled,
            budget, budget - p.BudgetAmount, ProjectMath.EffectiveBudgetHours(p.BudgetHours, co), ProjectMath.PercentComplete(tk), ProjectMath.Health(p, budget, labour + expense, overdue, open, today),
            tk.Count, tk.Count(x => x.Status == ProjectTaskStatus.Done), open, overdue, ms.Count(x => x.Status != MilestoneStatus.Cancelled), ms.Count(x => x.Status is MilestoneStatus.Achieved or MilestoneStatus.Invoiced));
    }
}
