using ERP.Application.Common;

namespace ERP.Modules.Projects.Features;

internal sealed class PreviewProjectBillHandler(BillingPlanner planner) : IQueryHandler<PreviewProjectBillQuery, BillPreviewDto>
{
    public async Task<BillPreviewDto> Handle(PreviewProjectBillQuery query, CancellationToken ct)
    {
        var plan = await planner.PlanAsync(query.Input, ct);
        var lines = plan.Lines.Select(l => new BillPreviewLineDto(l.Description, l.Quantity, l.Price, ProjectMath.Round(l.Quantity * l.Price))).ToList();
        return new BillPreviewDto(plan.Project.Id, lines, plan.Subtotal, plan.Discount, plan.Subtotal - plan.Discount, plan.Time.Count, plan.Milestones.Count, plan.Expenses.Count);
    }
}
