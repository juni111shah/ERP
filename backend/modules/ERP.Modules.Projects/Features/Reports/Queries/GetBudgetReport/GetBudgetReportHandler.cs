using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class GetBudgetReportHandler(
    IProjectRepository projects,
    IProjectTimesheetRepository timesheets,
    IProjectExpenseRepository expenses,
    IProjectMilestoneRepository milestones,
    IProjectTaskRepository tasks,
    IProjectChangeOrderRepository changeOrders,
    IProjectProgressBillingRepository progress,
    ProjectAccess access)
    : IQueryHandler<GetBudgetReportQuery, IReadOnlyList<BudgetRow>>
{
    public async Task<IReadOnlyList<BudgetRow>> Handle(GetBudgetReportQuery q, CancellationToken ct)
    {
        // 1. The live projects the caller may see, with everything that goes into their figures.
        var visible = await access.VisibleIdsAsync(ct);
        var list = (await projects.ListForBudgetReportAsync(ct)).Where(p => visible == null || visible.Contains(p.Id)).ToList();
        var ids = list.Select(p => p.Id).ToList();
        var ts = (await timesheets.ListNotRejectedOfProjectsAsync(ids, ct)).ToLookup(t => t.ProjectId);
        var ex = (await expenses.ListApprovedOfProjectsAsync(ids, ct)).ToLookup(e => e.ProjectId);
        var tk = (await tasks.ListOfProjectsAsync(ids, ct)).ToLookup(t => t.ProjectId);
        var co = (await changeOrders.ListForProjectsAsync(ids, ct)).ToLookup(c => c.ProjectId);
        var ms = (await milestones.ListInvoicedOfProjectsAsync(ids, ct)).ToLookup(m => m.ProjectId);
        var pr = (await progress.ListActiveOfProjectsAsync(ids, ct)).ToLookup(x => x.ProjectId);
        var today = DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow);

        // 2. One row per project.
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
            var fee = ReportMath.ContractFee(p, co[p.Id]);
            var earned = p.BillingType == ProjectBillingType.FixedFee ? Math.Round(fee * pct / 100m, 2) : 0m;
            var wip = p.BillingType == ProjectBillingType.FixedFee ? earned - billed : 0m;
            var tasksOpen = tk[p.Id].Where(t => t.Status is not (ProjectTaskStatus.Done or ProjectTaskStatus.Cancelled)).ToList();
            var overdue = tasksOpen.Count(t => t.DueDate is { } d && d < today);
            // earned value: what the finished share of the work was budgeted to cost, against the spend and against the plan
            var ev = Math.Round(budget * pct / 100m, 2);
            var share = ReportMath.PlannedShare(p.StartDate, p.EndDate, today);
            var pv = share is { } planned ? Math.Round(budget * planned, 2) : 0m;
            rows.Add(new BudgetRow(p.Id, p.Number, p.Name, p.Status, p.BillingType, budget, budget - p.BudgetAmount, cost, ReportMath.Percent(cost, budget), hoursBudget, logged, pct, forecast,
                budget > 0 ? budget - forecast : 0m, earned, billed, wip, ProjectMath.Health(p, budget, cost, overdue, tasksOpen.Count, today),
                pv, ev, cost > 0 && budget > 0 ? Math.Round(ev / cost, 2) : null, pv > 0 ? Math.Round(ev / pv, 2) : null, fee, fee > 0 ? fee - forecast : null));
        }
        return rows;
    }
}
