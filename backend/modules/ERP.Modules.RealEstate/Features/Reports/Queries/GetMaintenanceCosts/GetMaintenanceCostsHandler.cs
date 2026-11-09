using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Repositories;

namespace ERP.Modules.RealEstate.Features;

internal sealed class GetMaintenanceCostsHandler(IWorkOrderRepository workOrders, IPropertyRepository properties) : IQueryHandler<GetMaintenanceCostsQuery, MaintenanceCostsDto>
{
    public async Task<MaintenanceCostsDto> Handle(GetMaintenanceCostsQuery q, CancellationToken ct)
    {
        // 1. The work orders reported in the period and their property names.
        var entity = q.LegalEntityId;
        var orders = await workOrders.ListCostsReportedAsync(q.From, q.To, entity, ct);
        var propIds = orders.Select(o => o.PropertyId).Distinct().ToList();
        var names = await properties.NamesAsync(propIds, ct);

        // 2. One row per property and category, then the totals.
        bool IsOpen(WorkOrderStatus s) => s is not (WorkOrderStatus.Completed or WorkOrderStatus.Closed or WorkOrderStatus.Cancelled);
        var rows = orders.GroupBy(o => (o.PropertyId, Category: o.Category ?? "Uncategorised"))
            .Select(g => new MaintenanceRow(g.Key.PropertyId, names.GetValueOrDefault(g.Key.PropertyId, "?"), g.Key.Category, g.Count(), g.Count(o => IsOpen(o.Status)), g.Sum(o => o.EstimatedCost), g.Sum(o => o.ActualCost ?? 0)))
            .OrderBy(r => r.Property).ThenBy(r => r.Category).ToList();
        return new MaintenanceCostsDto(q.From, q.To, rows, rows.Sum(r => r.WorkOrders), rows.Sum(r => r.Open), rows.Sum(r => r.Estimated), rows.Sum(r => r.Actual));
    }
}
