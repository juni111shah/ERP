using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class GetProfitabilityReportHandler(
    IProjectRepository projects,
    IProjectTimesheetRepository timesheets,
    IProjectExpenseRepository expenses,
    IProjectMilestoneRepository milestones,
    IProjectChangeOrderRepository changeOrders,
    IProjectProgressBillingRepository progress,
    ProjectAccess access)
    : IQueryHandler<GetProfitabilityReportQuery, IReadOnlyList<ProfitabilityRow>>
{
    public async Task<IReadOnlyList<ProfitabilityRow>> Handle(GetProfitabilityReportQuery q, CancellationToken ct)
    {
        // 1. The projects the caller may see, and their time, expenses, milestones, progress billings and change orders of the period.
        var visible = await access.VisibleIdsAsync(ct);
        var list = (await projects.ListNonTemplatesAsync(q.ProjectId, ct)).Where(p => visible == null || visible.Contains(p.Id)).ToList();
        var ids = list.Select(p => p.Id).ToList();
        var ts = (await timesheets.ListNotRejectedOfProjectsBetweenAsync(ids, q.From, q.To, ct)).ToLookup(t => t.ProjectId);
        var ex = (await expenses.ListApprovedOfProjectsBetweenAsync(ids, q.From, q.To, ct)).ToLookup(e => e.ProjectId);
        // a milestone counts in the period it was reached, like time and expenses count on their own dates
        var ms = (await milestones.ListAchievedOrInvoicedOfProjectsAsync(ids, ct))
            .Where(m => (m.AchievedDate ?? m.DueDate) is var d && (q.From == null || d >= q.From) && (q.To == null || d <= q.To)).ToLookup(m => m.ProjectId);
        var pr = (await progress.ListActiveOfProjectsBetweenAsync(ids, q.From, q.To, ct)).ToLookup(x => x.ProjectId);
        var co = (await changeOrders.ListForProjectsAsync(ids, ct)).ToLookup(c => c.ProjectId);
        var wholeLife = q.From == null && q.To == null; // the contract is a whole-life figure: comparing it with one period's cost would mean nothing

        // 2. One row per project.
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
            var fee = ReportMath.ContractFee(p, co[p.Id]);
            decimal? contractMargin = wholeLife && fee > 0 ? fee - cost : null;
            rows.Add(new ProfitabilityRow(p.Id, p.Number, p.Name, p.CustomerName, p.Status, p.BillingType, t.Sum(x => x.Hours), t.Where(x => x.IsBillable && !x.WrittenOff).Sum(x => x.Hours),
                billed, unbilled, labour, expenseCost, cost, margin, ReportMath.Percent(margin, billed + unbilled), fee, contractMargin, contractMargin is { } cm ? ReportMath.Percent(cm, fee) : null));
        }
        return rows;
    }
}
