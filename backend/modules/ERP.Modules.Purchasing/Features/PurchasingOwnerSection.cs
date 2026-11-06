using ERP.Application.Abstractions;
using ERP.Application.Dashboard;
using ERP.Modules.Purchasing.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Purchasing.Features;

/// <summary>Purchase performance for the owner: spend committed in the period and the suppliers behind it.</summary>
public sealed class PurchasingOwnerSection(IReadRepository<PurchaseOrder> orders, ICurrentUser user) : IOwnerDashboardSection
{
    public string ModuleKey => PurchasingModule.Key;

    public async Task<IReadOnlyList<OwnerSection>> BuildAsync(OwnerScope scope, CancellationToken ct)
    {
        if (!user.HasPermission(P.OrdersRead)) return [];
        var entity = scope.LegalEntityId;
        var rows = await orders.Query().Where(o => o.Status != PurchaseOrderStatus.Cancelled && o.OrderDate >= scope.From && o.OrderDate <= scope.To && (entity == null || o.LegalEntityId == entity))
            .Select(o => new { o.VendorName, o.TotalAmount, o.Status, o.ExpectedDeliveryDate }).ToListAsync(ct);
        var late = rows.Count(o => o.ExpectedDeliveryDate < scope.Today && o.Status is PurchaseOrderStatus.Approved or PurchaseOrderStatus.PartiallyReceived);
        var top = rows.GroupBy(r => r.VendorName).Select(g => new { Name = g.Key, Value = g.Sum(r => r.TotalAmount), Count = g.Count() }).OrderByDescending(g => g.Value).Take(6).ToList();
        return
        [
            new("purchasing", "Purchase performance",
                [new("purchase-orders", "Orders in period", rows.Count, "number", Link: "/purchasing/orders"), new("purchase-value", "Order value", rows.Sum(r => r.TotalAmount)), new("purchase-late", "Late deliveries", late, "number")],
                new(["Supplier", "Orders", "Value"], top.Select(t => (IReadOnlyList<string>)[t.Name, t.Count.ToString(), t.Value.ToString("0.00")]).ToList(), ["text", "number", "money"])),
        ];
    }
}
