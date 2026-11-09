using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Manufacturing.Domain;
using ERP.Modules.Manufacturing.Repositories;

namespace ERP.Modules.Manufacturing.Features;

/// <summary>Rules shared by creating and updating a BOM: the catalog check, building a component line and the loop check.</summary>
internal sealed class BomComponents(IItemRepository items, IBillOfMaterialsRepository boms, IStockPostingService stock)
{
    /// <summary>Catalog rows (sku, name) of the produced item and every component; each must exist and be a product. Names and codes come from the catalog, never from the request.</summary>
    public async Task<Dictionary<Guid, (string Sku, string Name, decimal PurchasePrice)>> CatalogAsync(Guid producedItemId, IEnumerable<BomComponentInput>? components, CancellationToken ct)
    {
        var ids = (components ?? []).Select(c => c.ComponentItemId).Append(producedItemId).Distinct().ToList();
        var found = await items.ListProductCatalogAsync(ids, ct);
        var map = found.ToDictionary(i => i.Id, i => (i.Sku, i.Name, i.PurchasePrice ?? 0m));
        if (!map.ContainsKey(producedItemId)) throw new FieldValidationException("producedItemId", "Choose a product item to produce.");
        if (components is not null && components.Any(c => !map.ContainsKey(c.ComponentItemId))) throw new FieldValidationException("components", "A component was not found or is not a product.");
        return map;
    }

    /// <summary>The typed cost, or with none typed the item's current average (its purchase price before anything is in stock). Such a line keeps following the average on every read.</summary>
    public async Task<BomComponent> ComponentAsync(BillOfMaterials bom, BomComponentInput c, IReadOnlyDictionary<Guid, (string Sku, string Name, decimal PurchasePrice)> catalog, CancellationToken ct)
    {
        var useCurrent = c.UnitCost is null or <= 0;
        var cost = c.UnitCost ?? 0m;
        if (useCurrent)
        {
            var average = await stock.GetAverageCostAsync(c.ComponentItemId, null, ct);
            cost = average > 0 ? average : catalog[c.ComponentItemId].PurchasePrice;
        }
        return new BomComponent
        {
            LegalEntityId = bom.LegalEntityId,
            BomId = bom.Id,
            ComponentItemId = c.ComponentItemId,
            ComponentItemCode = catalog[c.ComponentItemId].Sku,
            ComponentItemName = catalog[c.ComponentItemId].Name,
            QuantityRequired = c.QuantityRequired,
            Uom = string.IsNullOrWhiteSpace(c.Uom) ? "PCS" : c.Uom.Trim(),
            ScrapPercentage = c.ScrapPercentage,
            UnitCost = cost,
            UseCurrentCost = useCurrent,
            Notes = c.Notes?.Trim()
        };
    }

    /// <summary>Refuses a BOM that would make its product, however many levels down, a component of itself. Only active BOMs count; reactivating one comes back through here.</summary>
    public async Task EnsureNoCycleAsync(Guid legalEntityId, Guid? bomId, Guid producedItemId, IEnumerable<Guid> componentIds, CancellationToken ct)
    {
        var others = await boms.ListOtherActiveWithComponentsAsync(legalEntityId, bomId, ct);
        var edges = new Dictionary<Guid, HashSet<Guid>>();
        var codes = new Dictionary<Guid, string>();
        foreach (var other in others)
        {
            if (!edges.TryGetValue(other.ProducedItemId, out var set)) edges[other.ProducedItemId] = set = [];
            codes[other.ProducedItemId] = other.ProducedItemCode;
            foreach (var c in other.Components) { set.Add(c.ComponentItemId); codes[c.ComponentItemId] = c.ComponentItemCode; }
        }

        foreach (var component in componentIds.Distinct())
        {
            if (BomStructure.FindPath(edges, component, producedItemId) is { } path)
                throw new FieldValidationException("components", $"This would make a loop: {string.Join(" uses ", path.Select(i => codes.GetValueOrDefault(i, i.ToString())))}, which this BOM would then use again.");
        }
    }
}
