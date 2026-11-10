using ERP.Application.Common;
using ERP.Modules.Inventory.Repositories;

namespace ERP.Modules.Inventory.Features;

internal sealed class GetReorderSuggestionsHandler(IStockPolicyRepository policies, IStockMovementRepository movements, IStockItemRepository items) : IQueryHandler<GetReorderSuggestionsQuery, ReorderDto>
{
    public async Task<ReorderDto> Handle(GetReorderSuggestionsQuery q, CancellationToken ct)
    {
        // 1. The reorder rules; none means nothing to suggest.
        var rules = await policies.ListAsync(ct: ct);
        if (rules.Count == 0) return new([]);

        // 2. Quantity on hand and details of the items with a rule.
        var ids = rules.Select(r => r.ItemId).ToList();
        var qty = await movements.QuantityByItemAsync(ids, ct);
        var byId = await items.MapInfoAsync(ids, ct);

        // 3. The items at or below their level, with what to order.
        var rows = rules.Where(r => qty.GetValueOrDefault(r.ItemId) <= r.ReorderLevel).Select(r =>
        {
            var onHand = qty.GetValueOrDefault(r.ItemId);
            var i = byId.GetValueOrDefault(r.ItemId);
            return new ReorderRow(i?.Sku ?? "?", i?.Name ?? "(deleted item)", i?.Uom ?? "", onHand, r.ReorderLevel, r.ReorderQuantity, Math.Max(r.ReorderQuantity, r.ReorderLevel - onHand));
        }).OrderBy(r => r.Sku).ToList();
        return new(rows);
    }
}
