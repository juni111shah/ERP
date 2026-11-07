using ERP.Application.Abstractions;
using ERP.Application.Dashboard;
using MediatR;

namespace ERP.Modules.Inventory.Features;

/// <summary>Stock value, slow-moving goods and shortages on the owner's view, from the same reports as the Inventory module.</summary>
public sealed class InventoryOwnerSection(ISender sender, ICurrentUser user) : IOwnerDashboardSection
{
    private const int SlowAfterDays = 60;

    public string ModuleKey => InventoryModule.Key;

    public async Task<IReadOnlyList<OwnerSection>> BuildAsync(OwnerScope scope, CancellationToken ct)
    {
        if (!user.HasPermission(InventoryModule.StockRead)) return [];
        var valuation = await sender.Send(new GetStockValuation(scope.To), ct);
        var slow = await sender.Send(new GetSlowMoving(scope.To, SlowAfterDays), ct);
        var reorder = await sender.Send(new GetReorderSuggestions(), ct);
        return
        [
            new("inventory", "Inventory", [new("inventory-value", "Inventory valuation", valuation.TotalValue, Link: "/inventory/stock"), new("inventory-lines", "Stock lines", valuation.Rows.Count, "number")]),
            new("slow-stock", "Slow-moving stock", [new("slow-value", $"Value idle over {SlowAfterDays} days", slow.TotalValue, Link: "/reports/slow-moving"), new("slow-items", "Items", slow.Rows.Count, "number")],
                new(["Item", "Quantity", "Value", "Days idle"], slow.Rows.Take(6).Select(r => (IReadOnlyList<string>)[$"{r.Sku} {r.Item}", r.Quantity.ToString("0.##"), r.Value.ToString("0.00"), r.DaysIdle.ToString()]).ToList(), ["text", "number", "money", "number"])),
            new("shortages", "Shortages", [new("short-items", "Items at or below reorder level", reorder.Rows.Count, "number", Link: "/reports/reorder-suggestions")],
                new(["Item", "On hand", "Reorder level", "Suggested order"], reorder.Rows.Take(6).Select(r => (IReadOnlyList<string>)[$"{r.Sku} {r.Item}", r.OnHand.ToString("0.##"), r.ReorderLevel.ToString("0.##"), r.SuggestedOrder.ToString("0.##")]).ToList(), ["text", "number", "number", "number"])),
        ];
    }
}
