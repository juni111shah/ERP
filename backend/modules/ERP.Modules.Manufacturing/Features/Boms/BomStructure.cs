using ERP.Application.Abstractions;
using ERP.Modules.Manufacturing.Domain;

namespace ERP.Modules.Manufacturing.Features;

/// <summary>Which items are made from which. Shared by the cycle check on saving a BOM and the requirements explosion.</summary>
public static class BomStructure
{
    /// <summary>A way from <paramref name="from"/> down to <paramref name="to"/> through the edges (parent item to its components), or null. Both ends are included.</summary>
    public static List<Guid>? FindPath(IReadOnlyDictionary<Guid, HashSet<Guid>> edges, Guid from, Guid to)
    {
        var seen = new HashSet<Guid>();
        var path = new List<Guid>();
        return Walk(from) ? path : null;

        bool Walk(Guid item)
        {
            path.Add(item);
            if (item == to) return true;
            if (seen.Add(item) && edges.TryGetValue(item, out var children))
                foreach (var child in children)
                    if (Walk(child)) return true;
            path.RemoveAt(path.Count - 1);
            return false;
        }
    }

    /// <summary>Any loop in the edges as the items going round it, first item repeated at the end; null when there is none.</summary>
    public static List<Guid>? FindCycle(IReadOnlyDictionary<Guid, HashSet<Guid>> edges)
    {
        var done = new HashSet<Guid>();
        var path = new List<Guid>();
        foreach (var start in edges.Keys)
            if (Walk(start)) return path;
        return null;

        bool Walk(Guid item)
        {
            if (path.Contains(item))
            {
                path.RemoveRange(0, path.IndexOf(item));
                path.Add(item);
                return true;
            }
            if (!done.Add(item)) return false;
            path.Add(item);
            if (edges.TryGetValue(item, out var children))
                foreach (var child in children)
                    if (Walk(child)) return true;
            path.RemoveAt(path.Count - 1);
            return false;
        }
    }

    /// <summary>Components needed for an output quantity of this BOM, wastage included.</summary>
    public static decimal Required(BillOfMaterials bom, BomComponent component, decimal outputQuantity)
        => Math.Round(component.QuantityRequired * (outputQuantity / (bom.OutputQuantity > 0 ? bom.OutputQuantity : 1)) * (1 + component.ScrapPercentage / 100m), 4);

    /// <summary>
    /// The cost each component should be read at: its typed cost, or the item's current average where none was typed.
    /// A component with no average (nothing received yet, Inventory off) keeps the snapshot taken when the BOM was saved.
    /// </summary>
    public static async Task<Dictionary<Guid, decimal>> CurrentCostsAsync(IEnumerable<BomComponent> components, IStockPostingService stock, Guid? warehouseId, CancellationToken ct)
    {
        var result = new Dictionary<Guid, decimal>();
        foreach (var c in components)
        {
            var cost = c.UnitCost;
            if (c.UseCurrentCost)
            {
                var current = await stock.GetAverageCostAsync(c.ComponentItemId, warehouseId, ct);
                if (current > 0) cost = current;
            }
            result[c.Id] = cost;
        }
        return result;
    }

    public static decimal MaterialCost(IEnumerable<BomComponent> components, IReadOnlyDictionary<Guid, decimal> costs)
        => components.Sum(c => c.QuantityRequired * costs[c.Id] * (1 + c.ScrapPercentage / 100m));
}
