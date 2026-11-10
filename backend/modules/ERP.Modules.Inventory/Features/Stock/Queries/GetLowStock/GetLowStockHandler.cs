using ERP.Application.Common;
using ERP.Modules.Inventory.Repositories;

namespace ERP.Modules.Inventory.Features;

internal sealed class GetLowStockHandler(IStockPolicyRepository policies, IStockMovementRepository movements, IStockItemRepository items) : IQueryHandler<GetLowStockQuery, IReadOnlyList<LowStockRow>>
{
    public async Task<IReadOnlyList<LowStockRow>> Handle(GetLowStockQuery q, CancellationToken ct)
    {
        // 1. The reorder rules; none means nothing is low.
        var rules = await policies.ListAsync(ct: ct);
        if (rules.Count == 0) return [];

        // 2. Quantity on hand and names of the items with a rule.
        var ids = rules.Select(r => r.ItemId).ToList();
        var qty = await movements.QuantityByItemAsync(ids, ct);
        var map = await items.MapNamesAsync(ids, ct);

        // 3. The items at or below their level.
        return rules.Where(r => qty.GetValueOrDefault(r.ItemId) <= r.ReorderLevel).Select(r => new LowStockRow(r.ItemId, map.GetValueOrDefault(r.ItemId)?.Sku ?? "?", map.GetValueOrDefault(r.ItemId)?.Name ?? "(deleted item)", qty.GetValueOrDefault(r.ItemId), r.ReorderLevel, r.ReorderQuantity)).OrderBy(r => r.Sku).ToList();
    }
}
