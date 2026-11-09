using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

/// <summary>What billing a project would put on the invoice. Shared by the preview and the real billing.</summary>
public sealed record BillingPlan(Project Project, List<ProjectTimesheet> Time, List<ProjectMilestone> Milestones, List<ProjectExpense> Expenses, List<BillingLines.Line> Lines, decimal Subtotal, decimal Discount, decimal ProgressAmount);

/// <summary>Checks the project and picks what is billable: approved, unbilled, not written off. The rows come back tracked so billing can mark them.</summary>
public sealed class BillingPlanner(
    IProjectRepository projects,
    IProjectTimesheetRepository timesheets,
    IProjectMilestoneRepository milestones,
    IProjectExpenseRepository expenses,
    IProjectProgressBillingRepository progressRows,
    IProjectChangeOrderRepository changeOrders,
    IProjectTaskRepository tasks,
    ProjectAccess access)
{
    public async Task<BillingPlan> PlanAsync(BillProjectInput i, CancellationToken ct)
    {
        var p = await projects.GetAsync(i.ProjectId, ct);
        await access.EnsureAsync(p, ct);
        if (p.IsTemplate) throw new ConflictException($"'{p.Name}' is a template and cannot be invoiced.");
        if (p.BillingType == ProjectBillingType.NonBillable)
            throw new ConflictException($"Project '{p.Name}' is non-billable and cannot be invoiced.");
        if (p.Status is ProjectStatus.Planning or ProjectStatus.Cancelled)
            throw new ConflictException($"Project '{p.Name}' is {p.Status.ToString().ToLowerInvariant()} and cannot be invoiced.");
        if (!p.CustomerId.HasValue)
            throw new FieldValidationException("customerId", $"Project '{p.Name}' has no customer assigned. An accounting invoice cannot be generated.");
        if (i.ProgressPercent is not null && p.BillingType != ProjectBillingType.FixedFee)
            throw new FieldValidationException("progressPercent", "Progress billing is only for fixed-fee projects.");

        var time = p.BillingType == ProjectBillingType.FixedFee ? new List<ProjectTimesheet>() : await timesheets.ListChosenReadyToBillForUpdateAsync(p.Id, i.TimesheetIds, ct); // a fixed-fee project bills milestones only
        var ms = await milestones.ListChosenAchievedForUpdateAsync(p.Id, i.MilestoneIds, ct);
        var ex = await expenses.ListChosenReadyToBillForUpdateAsync(p.Id, i.ExpenseIds, ct);

        decimal progressAmount = 0m;
        if (i.ProgressPercent is { } pct)
        {
            var fee = ProjectMath.Fee(p, await changeOrders.ListForProjectAsync(p.Id, ct));
            if (fee <= 0) throw new ConflictException("The project has no fixed fee to bill a percentage of.");
            var already = await progressRows.SumActiveAsync(p.Id, ct);
            var invoicedMilestones = await milestones.SumInvoicedAsync(p.Id, ct);
            progressAmount = ProjectMath.Round(fee * pct / 100m);
            var thisMilestones = ms.Sum(m => m.Amount);
            if (already + invoicedMilestones + thisMilestones + progressAmount > fee)
                throw new ConflictException($"That would bill {already + invoicedMilestones + thisMilestones + progressAmount:N2}, more than the fixed fee of {fee:N2}.");
        }

        if (time.Count == 0 && ms.Count == 0 && ex.Count == 0 && progressAmount == 0)
            throw new ConflictException("No unbilled timesheets or achieved milestones found to bill.");

        var taskIds = time.Where(t => t.TaskId != null).Select(t => t.TaskId!.Value).Distinct().ToList();
        var titles = taskIds.Count == 0 ? new Dictionary<Guid, string>() : await tasks.MapTitlesAsync(taskIds, ct);
        var lines = BillingLines.Build(p, time, ms, ex, titles, i.Grouping, progressAmount, i.ProgressPercent);
        var subtotal = ProjectMath.Round(lines.Sum(l => l.Quantity * l.Price));
        var discount = BillingLines.Discount(subtotal, i.DiscountPercent);
        return new BillingPlan(p, time, ms, ex, lines, subtotal, discount, progressAmount);
    }
}
