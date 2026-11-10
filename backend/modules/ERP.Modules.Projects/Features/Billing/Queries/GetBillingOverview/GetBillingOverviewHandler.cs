using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class GetBillingOverviewHandler(
    IProjectRepository projects,
    IProjectTimesheetRepository timesheets,
    IProjectMilestoneRepository milestones,
    IProjectExpenseRepository expenses,
    IProjectProgressBillingRepository progressRows,
    IProjectChangeOrderRepository changeOrders,
    ProjectAccess access)
    : IQueryHandler<GetBillingOverviewQuery, IReadOnlyList<ProjectBillingSummaryDto>>
{
    public async Task<IReadOnlyList<ProjectBillingSummaryDto>> Handle(GetBillingOverviewQuery query, CancellationToken ct)
    {
        var visible = await access.VisibleIdsAsync(ct);
        // Only the columns the summary needs, grouped by project once (no per-project rescans of whole tables).
        var approvedTime = (await timesheets.ListUnbilledAsync(ct));
        var unbilled = approvedTime.Where(x => x.ApprovalStatus == TimesheetStatus.Approved).GroupBy(x => x.ProjectId).ToDictionary(g => g.Key, g => (Hours: g.Sum(x => x.Hours), Amount: g.Sum(x => x.TotalAmount)));
        var waiting = approvedTime.Where(x => x.ApprovalStatus != TimesheetStatus.Approved).GroupBy(x => x.ProjectId).ToDictionary(g => g.Key, g => g.Sum(x => x.TotalAmount));
        var achieved = (await milestones.ListAchievedAmountsAsync(ct))
            .GroupBy(x => x.ProjectId).ToDictionary(g => g.Key, g => (Count: g.Count(), Amount: g.Sum(x => x.Amount)));
        var costs = (await expenses.ListAllReadyToBillAsync(ct))
            .GroupBy(x => x.ProjectId).ToDictionary(g => g.Key, g => g.Sum(BillingLines.Charge));
        var billable = unbilled.Keys.Concat(achieved.Keys).Concat(costs.Keys).ToHashSet();
        var allProjects = await projects.ListBillingCandidatesAsync(billable, ct);
        var billedFixed = (await progressRows.ListActiveAsync(ct)).GroupBy(x => x.ProjectId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
        var invoicedMs = (await milestones.ListInvoicedAmountsAsync(ct)).GroupBy(x => x.ProjectId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
        var changes = (await changeOrders.ListApprovedAsync(ct)).GroupBy(x => x.ProjectId).ToDictionary(g => g.Key, g => g.Sum(x => x.AmountChange));
        var list = new List<ProjectBillingSummaryDto>();
        foreach (var p in allProjects)
        {
            if (p.BillingType == ProjectBillingType.NonBillable || (visible != null && !visible.Contains(p.Id))) continue;
            var (hours, hoursAmount) = p.BillingType == ProjectBillingType.FixedFee ? default : unbilled.GetValueOrDefault(p.Id); // a fixed-fee project bills milestones only
            var (msCount, msAmount) = achieved.GetValueOrDefault(p.Id);
            var expenseAmount = costs.GetValueOrDefault(p.Id);
            var remaining = p.BillingType == ProjectBillingType.FixedFee
                ? Math.Max(0m, (p.ContractValue ?? p.BudgetAmount) + changes.GetValueOrDefault(p.Id) - billedFixed.GetValueOrDefault(p.Id) - invoicedMs.GetValueOrDefault(p.Id) - msAmount) : 0m;
            var total = hoursAmount + msAmount + expenseAmount;
            if (total > 0 || p.Status == ProjectStatus.Active)
                list.Add(new ProjectBillingSummaryDto(p.Id, p.Number, p.Name, p.BillingType, hours, hoursAmount, msCount, msAmount, total, expenseAmount, p.BillingType == ProjectBillingType.FixedFee ? 0m : waiting.GetValueOrDefault(p.Id), remaining));
        }
        return list;
    }
}
