using ERP.Application.Abstractions;
using ERP.Application.Dashboard;
using ERP.Modules.Manufacturing.Domain;

namespace ERP.Modules.Manufacturing.Features;

public sealed class ManufacturingDashboard(
    IReadRepository<BillOfMaterials> boms,
    IReadRepository<WorkOrder> workOrders,
    ICurrentUser user) : IDashboardContributor
{
    public string ModuleKey => ManufacturingModule.Key;

    public async Task<DashboardContribution> BuildAsync(DashboardScope scope, CancellationToken ct)
    {
        var entity = scope.LegalEntityId;
        var kpis = new List<KpiCard>();

        var charts = new List<DashboardChart>();

        if (user.HasPermission(Permissions.BomsRead))
        {
            var activeBoms = await boms.CountAsync(b => b.IsActive && (entity == null || b.LegalEntityId == entity), ct);
            kpis.Add(new("mfg-active-boms", "Active BOMs", activeBoms, "number", "production recipes", 170, "/manufacturing/boms"));
        }

        if (user.HasPermission(Permissions.WorkOrdersRead))
        {
            var activeWorkOrders = await workOrders.CountAsync(w => (w.Status == WorkOrderStatus.Planned || w.Status == WorkOrderStatus.InProgress) && (entity == null || w.LegalEntityId == entity), ct);
            kpis.Add(new("mfg-open-workorders", "Open Work Orders", activeWorkOrders, "number", "orders in production pipeline", 175, "/manufacturing/workorders"));

            var statuses = new[] { WorkOrderStatus.Draft, WorkOrderStatus.Planned, WorkOrderStatus.InProgress, WorkOrderStatus.Completed, WorkOrderStatus.Cancelled };
            var counts = new List<decimal>();
            foreach (var st in statuses)
            {
                var c = await workOrders.CountAsync(w => w.Status == st && (entity == null || w.LegalEntityId == entity), ct);
                counts.Add((decimal)c);
            }
            charts.Add(new("mfg-production", "Work orders by status", "bar",
                ["Draft", "Planned", "In Progress", "Completed", "Cancelled"],
                [new("Work Orders", counts)], "Manufacturing floor status distribution", 45));
        }

        return new DashboardContribution(kpis, charts, []);
    }
}
