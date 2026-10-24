using ERP.Application.Abstractions;
using ERP.Application.Dashboard;
using ERP.Modules.Purchasing.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Purchasing.Features;

public sealed class PurchasingDashboard(
    IReadRepository<PurchaseOrder> orders,
    IReadRepository<PurchaseRequisition> requisitions,
    ICurrentUser user) : IDashboardContributor
{
    public string ModuleKey => PurchasingModule.Key;

    public async Task<DashboardContribution> BuildAsync(DashboardScope scope, CancellationToken ct)
    {
        var entity = scope.LegalEntityId;
        var kpis = new List<KpiCard>();
        var charts = new List<DashboardChart>();

        if (user.HasPermission(P.OrdersRead))
        {
            var openOrdersQuery = orders.Query().Where(o => (o.Status == PurchaseOrderStatus.Approved
                || o.Status == PurchaseOrderStatus.PartiallyReceived)
                && (entity == null || o.LegalEntityId == entity));
            var openOrdersCount = await openOrdersQuery.CountAsync(ct);
            var openOrdersAmount = await openOrdersQuery.SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0m;

            kpis.Add(new("open-purchase-orders", "Open Procurement", openOrdersCount, "number", $"pending PO value {openOrdersAmount:N0}", 125, "/purchasing/orders"));

            var poStatuses = new[] { PurchaseOrderStatus.Draft, PurchaseOrderStatus.Approved, PurchaseOrderStatus.PartiallyReceived, PurchaseOrderStatus.Received, PurchaseOrderStatus.Cancelled };
            var poCounts = new List<decimal>();
            foreach (var st in poStatuses)
            {
                var c = await orders.CountAsync(o => o.Status == st && (entity == null || o.LegalEntityId == entity), ct);
                poCounts.Add((decimal)c);
            }
            charts.Add(new("purchasing-orders-status", "Procurement orders by status", "hbar",
                ["Draft", "Approved", "Partially Received", "Received", "Cancelled"],
                [new("Purchase Orders", poCounts)], "Vendor procurement status distribution", 40));
        }

        if (user.HasPermission(P.RequisitionsRead))
        {
            var pendingReqs = await requisitions.CountAsync(r => (r.Status == PurchaseRequisitionStatus.Draft
                || r.Status == PurchaseRequisitionStatus.PendingApproval)
                && (entity == null || r.LegalEntityId == entity), ct);
            kpis.Add(new("pending-requisitions", "Requisitions", pendingReqs, "number", "awaiting approval/PO", 128, "/purchasing/requisitions"));
        }

        return new DashboardContribution(kpis, charts, []);
    }
}
