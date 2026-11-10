using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class GetBillableWorkHandler(
    IProjectRepository projects,
    IProjectTimesheetRepository timesheets,
    IProjectMilestoneRepository milestones,
    IProjectExpenseRepository expenses,
    IProjectProgressBillingRepository progressRows,
    IProjectChangeOrderRepository changeOrders,
    IProjectTaskRepository tasks,
    ProjectAccess access)
    : IQueryHandler<GetBillableWorkQuery, BillableWorkDto>
{
    public async Task<BillableWorkDto> Handle(GetBillableWorkQuery query, CancellationToken ct)
    {
        // 1. Load the project.
        var p = await projects.GetAsync(query.ProjectId, ct);
        await access.EnsureAsync(p, ct);
        var id = p.Id;

        // 2. What is ready to bill: time (time-and-materials only), expenses and achieved milestones.
        var time = p.BillingType == ProjectBillingType.TimeAndMaterials
            ? await timesheets.ListReadyToBillAsync(id, ct)
            : [];
        var taskIds = time.Where(t => t.TaskId != null).Select(t => t.TaskId!.Value).Distinct().ToList();
        var titles = taskIds.Count == 0 ? new Dictionary<Guid, string>() : await tasks.MapTitlesAsync(taskIds, ct);
        var costs = await expenses.ListReadyToBillAsync(id, ct);
        var ms = await milestones.ListAchievedAsync(id, ct);

        // 3. What is left of a fixed fee.
        var fee = ProjectMath.Fee(p, await changeOrders.ListForProjectAsync(id, ct));
        var billed = await progressRows.SumActiveAsync(id, ct)
            + await milestones.SumInvoicedAsync(id, ct);
        var remaining = p.BillingType == ProjectBillingType.FixedFee ? Math.Max(0m, fee - billed - ms.Sum(m => m.Amount)) : 0m;
        return new BillableWorkDto(id, p.BillingType, fee, remaining,
            time.Select(t => new BillableTimeDto(t.Id, t.Date, t.WorkerName, t.TaskId is { } tid ? titles.GetValueOrDefault(tid) : null, t.Description, t.Hours, t.TotalAmount)).ToList(),
            costs.Select(e => new BillableExpenseDto(e.Id, e.Date, e.Category, e.Description, BillingLines.Charge(e))).ToList(),
            ms.Select(m => new BillableMilestoneDto(m.Id, m.Title, m.AchievedDate, m.Amount)).ToList());
    }
}
