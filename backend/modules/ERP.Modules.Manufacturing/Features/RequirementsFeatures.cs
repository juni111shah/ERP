using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Catalog;
using ERP.Modules.Manufacturing.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Manufacturing.Features;

/// <summary>
/// One item's position. GrossRequired is what open orders (and any what-if quantity) need; NetRequired is what is left after on-hand, on-order and in-production,
/// and is to be made when IsManufactured, otherwise bought. Level 0 is what is being produced; deeper levels are its components.
/// </summary>
public record MaterialRequirementDto(
    Guid ItemId,
    string ItemCode,
    string ItemName,
    string Uom,
    int Level,
    bool IsManufactured,
    decimal GrossRequired,
    decimal OnHand,
    decimal OnOrder,
    decimal InProduction,
    decimal NetRequired);

/// <summary>OpenPurchaseNetted is false until something exposes open purchase quantities to this module: OnOrder is then zero and shortages may be overstated.</summary>
public record MaterialRequirementsDto(
    Guid LegalEntityId,
    Guid? WarehouseId,
    int OpenWorkOrders,
    bool OpenPurchaseNetted,
    List<MaterialRequirementDto> Rows,
    List<MaterialRequirementDto> Shortages);

/// <summary>ItemId with Quantity adds a what-if demand for that many of an item through its active BOM, on top of the open orders.</summary>
public record GetMaterialRequirementsQuery(Guid LegalEntityId, Guid? WarehouseId = null, Guid? ItemId = null, decimal? Quantity = null) : IQuery<MaterialRequirementsDto>;

public class RequirementsHandlers(
    IReadRepository<WorkOrder> workOrders,
    IReadRepository<BillOfMaterials> boms,
    IReadRepository<Item> items,
    IStockPostingService stock,
    ERP.Application.Organizations.ILegalEntityGuard guard) :
    IRequestHandler<GetMaterialRequirementsQuery, MaterialRequirementsDto>
{
    public async Task<MaterialRequirementsDto> Handle(GetMaterialRequirementsQuery request, CancellationToken ct)
    {
        await guard.RequireAsync(request.LegalEntityId, ct);
        if (request.Quantity is <= 0) throw new FieldValidationException("quantity", "Quantity must be greater than zero.");
        if (request.ItemId.HasValue != request.Quantity.HasValue) throw new FieldValidationException("itemId", "Give an item and a quantity together.");

        var entity = request.LegalEntityId;
        var warehouse = request.WarehouseId;
        var open = await workOrders.Query(new Specification<WorkOrder>(w => w.LegalEntityId == entity
                && (w.Status == WorkOrderStatus.Draft || w.Status == WorkOrderStatus.Planned || w.Status == WorkOrderStatus.InProgress)
                && (warehouse == null || w.WarehouseId == warehouse)))
            .Include(w => w.MaterialIssues).ToListAsync(ct);
        open = open.Where(w => Remaining(w) > 0).ToList();

        // An order explodes through the BOM it was raised from, even if that BOM has since been deactivated; sub-assemblies through their active BOM.
        var orderBomIds = open.Select(w => w.BomId).Distinct().ToList();
        var loaded = await boms.Query(new Specification<BillOfMaterials>(b => b.LegalEntityId == entity && (b.IsActive || orderBomIds.Contains(b.Id))))
            .Include(b => b.Components).ToListAsync(ct);
        var bomById = loaded.ToDictionary(b => b.Id);
        var activeByItem = loaded.Where(b => b.IsActive).GroupBy(b => b.ProducedItemId).ToDictionary(g => g.Key, g => g.OrderByDescending(b => b.CreatedAt).First());

        // Walk the structure first: a loop would make the explosion endless and any figure meaningless, so it is reported instead.
        var edges = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var bom in loaded)
        {
            if (!edges.TryGetValue(bom.ProducedItemId, out var set)) edges[bom.ProducedItemId] = set = [];
            set.UnionWith(bom.Components.Select(c => c.ComponentItemId));
        }
        if (BomStructure.FindCycle(edges) is { } cycle)
        {
            var names = loaded.Select(b => (b.ProducedItemId, b.ProducedItemCode)).Concat(loaded.SelectMany(b => b.Components).Select(c => (c.ComponentItemId, c.ComponentItemCode)))
                .GroupBy(x => x.Item1).ToDictionary(g => g.Key, g => g.First().Item2);
            throw new ConflictException($"These bills of materials form a loop: {string.Join(" > ", cycle.Select(i => names.GetValueOrDefault(i, i.ToString())))}. Correct one of them to plan requirements.");
        }

        var gross = new Dictionary<Guid, decimal>();
        void Need(Guid item, decimal quantity) => gross[item] = gross.GetValueOrDefault(item) + quantity;

        foreach (var w in open.Where(w => bomById.ContainsKey(w.BomId)))
        {
            var bom = bomById[w.BomId];
            foreach (var c in bom.Components)
            {
                var issued = w.MaterialIssues.Where(i => i.ItemId == c.ComponentItemId && i.ReversedAt is null).Sum(i => i.Quantity);
                var still = BomStructure.Required(bom, c, w.PlannedQuantity) - issued;
                if (still > 0) Need(c.ComponentItemId, still);
            }
        }
        if (request.ItemId is { } whatIf) Need(whatIf, request.Quantity!.Value);

        var level = Levels(open.Select(w => w.ProducedItemId).Concat(gross.Keys).Distinct(), edges);
        var inProduction = open.GroupBy(w => w.ProducedItemId).ToDictionary(g => g.Key, g => g.Sum(Remaining));
        var onHand = await OnHandAsync(level.Keys.ToList(), open, warehouse, entity, ct);

        // Parents before children, so each item's gross is complete before it is netted and passed down.
        var rows = new List<(Guid Item, int Level, bool Made, decimal Gross, decimal OnHand, decimal InProduction, decimal Net)>();
        foreach (var itemId in level.OrderBy(l => l.Value).Select(l => l.Key))
        {
            if (!gross.TryGetValue(itemId, out var required) || required <= 0) continue;
            activeByItem.TryGetValue(itemId, out var bom);
            var have = onHand.GetValueOrDefault(itemId);
            var making = inProduction.GetValueOrDefault(itemId);
            var net = Math.Max(0m, required - Math.Max(have, 0m) - making);
            rows.Add((itemId, level[itemId], bom is not null, Math.Round(required, 4), have, making, Math.Round(net, 4)));
            if (net > 0 && bom is not null)
                foreach (var c in bom.Components) Need(c.ComponentItemId, BomStructure.Required(bom, c, net));
        }

        var ids = rows.Select(r => r.Item).ToList();
        var catalog = await items.Query(new Specification<Item>(i => ids.Contains(i.Id))).Select(i => new { i.Id, i.Sku, i.Name, i.Uom }).ToDictionaryAsync(i => i.Id, ct);
        var result = rows.Select(r =>
        {
            catalog.TryGetValue(r.Item, out var item);
            return new MaterialRequirementDto(r.Item, item?.Sku ?? string.Empty, item?.Name ?? string.Empty, item?.Uom ?? string.Empty, r.Level, r.Made, r.Gross, r.OnHand, 0m, r.InProduction, r.Net);
        }).ToList();

        return new MaterialRequirementsDto(entity, warehouse, open.Count, false, result, result.Where(r => r.NetRequired > 0).ToList());
    }

    private static decimal Remaining(WorkOrder w) => w.PlannedQuantity - w.CompletedQuantity - w.ScrappedQuantity;

    /// <summary>Each item's deepest level below the roots, so an item used at two depths is netted once, after everything that needs it. The edges are known to hold no loop.</summary>
    private static Dictionary<Guid, int> Levels(IEnumerable<Guid> roots, IReadOnlyDictionary<Guid, HashSet<Guid>> edges)
    {
        var level = new Dictionary<Guid, int>();
        void Visit(Guid item, int depth)
        {
            if (level.TryGetValue(item, out var known) && known >= depth) return;
            level[item] = depth;
            if (edges.TryGetValue(item, out var children))
                foreach (var child in children) Visit(child, depth + 1);
        }
        var rootList = roots.ToList();
        var nested = rootList.Where(r => edges.Any(e => e.Value.Contains(r))).ToHashSet();
        foreach (var root in rootList.Where(r => !nested.Contains(r))) Visit(root, 0);
        foreach (var root in nested.Where(r => !level.ContainsKey(r))) Visit(root, 0);
        return level;
    }

    /// <summary>The stock service answers per warehouse, so the figure covers the asked warehouse, else the warehouses the open orders draw from, else the entity's first.</summary>
    private async Task<Dictionary<Guid, decimal>> OnHandAsync(List<Guid> itemIds, List<WorkOrder> open, Guid? warehouse, Guid entity, CancellationToken ct)
    {
        var warehouses = warehouse is { } one ? [one] : open.Select(w => (Guid?)w.WarehouseId).Distinct().ToList();
        if (warehouses.Count == 0) warehouses.Add(null);

        var total = new Dictionary<Guid, decimal>();
        foreach (var w in warehouses)
            foreach (var (item, quantity) in await stock.GetOnHandAsync(itemIds, w, entity, ct))
                total[item] = total.GetValueOrDefault(item) + quantity;
        return total;
    }
}
