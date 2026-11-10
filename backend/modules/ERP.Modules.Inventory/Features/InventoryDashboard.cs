using ERP.Application.Abstractions;
using MediatR;

namespace ERP.Modules.Inventory.Features;

/// <summary>Stock value and low-stock count on the overview.</summary>
public sealed class InventoryDashboard(ISender sender, ICurrentUser user) : ERP.Application.Dashboard.IDashboardContributor
{
    public string ModuleKey => InventoryModule.Key;

    public async Task<ERP.Application.Dashboard.DashboardContribution> BuildAsync(ERP.Application.Dashboard.DashboardScope scope, CancellationToken ct)
    {
        if (!user.HasPermission(InventoryModule.StockRead)) return ERP.Application.Dashboard.DashboardContribution.Empty;
        var stock = await sender.Send(new GetStockOnHandQuery(null, null), ct);
        var low = await sender.Send(new GetLowStockQuery(), ct);
        var alerts = low.Count == 0 ? [] : new List<ERP.Application.Dashboard.DashboardAlert> { new("warning", "Inventory", "action", $"{low.Count} item(s) at or below their reorder level", "/inventory/stock", 40) };
        return new([new("stock-value", "Stock value", stock.TotalValue, "money", $"{stock.Rows.Count} item/warehouse lines", 120, "/inventory/stock")], [], alerts);
    }
}
