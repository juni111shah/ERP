using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Modules.Manufacturing.Domain;
using ERP.Modules.Manufacturing.Repositories;

namespace ERP.Modules.Manufacturing.Features;

/// <summary>Moving material into and out of a work order, shared by release, issue, completion, cancellation and reversal.</summary>
internal sealed class WorkOrderMaterials(
    IRepository<WorkOrderMaterialIssue> issues,
    IItemRepository items,
    IJournalEntryRepository journals,
    IStockPostingService stockPosting,
    IPostingService posting,
    ISettingsService settings,
    IClock clock)
{
    /// <summary>What the BOM still needs for this output, wastage included, less what has already been issued. Every component must be a stocked product.</summary>
    public async Task<List<MaterialNeed>> BomNeedsAsync(WorkOrder wo, BillOfMaterials bom, decimal output, CancellationToken ct)
    {
        var ids = bom.Components.Select(c => c.ComponentItemId).Distinct().ToList();
        var products = await items.GetProductsAsync(ids, ct);
        var net = WorkOrderRules.NetIssued(wo);
        var needs = new List<MaterialNeed>();
        foreach (var c in bom.Components)
        {
            if (!products.ContainsKey(c.ComponentItemId))
                throw new ConflictException($"Component item '{c.ComponentItemCode}' was not found or is not a Product.");
            var still = BomStructure.Required(bom, c, output) - (net.TryGetValue(c.ComponentItemId, out var issued) ? issued.Quantity : 0m);
            if (still > 0) needs.Add(new MaterialNeed(c.ComponentItemId, c.ComponentItemCode, c.ComponentItemName, still));
        }
        return needs;
    }

    /// <summary>Dr WIP, Cr inventory, at the cost the stock service issues at. A line that moved no stock (Inventory off) posts nothing.</summary>
    public async Task IssueAsync(WorkOrder wo, IReadOnlyList<MaterialNeed> needs, DateOnly date, bool backflushed, string? notes, CancellationToken ct)
    {
        if (needs.Count == 0) return;
        await EnsureAvailableAsync(wo, needs, ct);

        decimal total = 0;
        foreach (var need in needs)
        {
            var unitCost = await stockPosting.GetAverageCostAsync(need.ItemId, wo.WarehouseId, ct);
            var moved = await stockPosting.IssueStockAsync(new StockIssueRequest(
                wo.LegalEntityId, need.ItemId, need.Quantity, date, wo.WorkOrderNumber,
                $"Consumed for WO {wo.WorkOrderNumber} ({need.Code})", wo.WarehouseId, SkipJournal: true), ct);
            if (moved is null) continue;

            var value = Math.Round(need.Quantity * unitCost, 2);
            total += value;
            issues.Add(new WorkOrderMaterialIssue
            {
                LegalEntityId = wo.LegalEntityId,
                WorkOrderId = wo.Id,
                ItemId = need.ItemId,
                ItemCode = need.Code,
                ItemName = need.Name,
                Quantity = need.Quantity,
                UnitCost = unitCost,
                Value = value,
                IssueDate = date,
                Backflushed = backflushed,
                Notes = (need.Notes ?? notes)?.Trim(),
            });
        }

        if (total > 0)
        {
            var memo = $"Material issued to WO {wo.WorkOrderNumber}";
            await posting.PostAsync(new PostingRequest(wo.LegalEntityId, date, WorkOrderRules.JournalSource, wo.Id, memo, wo.WorkOrderNumber,
            [
                new(await posting.AccountForRoleAsync(SystemRoles.Wip, ct), total, 0, memo),
                new(await posting.AccountForRoleAsync("inventory", ct), 0, total, memo),
            ]), ct);
        }
    }

    /// <summary>Puts everything still on the order back on the shelf at the cost it left at. The ledger side is the reversal of the order's journals.</summary>
    public async Task UndoMaterialAsync(WorkOrder wo, string memo, CancellationToken ct)
    {
        var net = WorkOrderRules.NetIssued(wo).Where(n => n.Value.Quantity > 0).ToList();
        if (net.Count > 0 && !await stockPosting.IsInventoryEnabledAsync(ct))
            throw new ConflictException("Turn the Inventory module on first: this work order moved stock that has to go back.");

        foreach (var (itemId, issued) in net)
        {
            await stockPosting.ReceiveStockAsync(new StockReceiveRequest(
                wo.LegalEntityId, itemId, issued.Quantity, Math.Round(issued.Value / issued.Quantity, 4), WorkOrderRules.Today,
                $"{wo.WorkOrderNumber}-REV", memo, wo.WarehouseId, SkipJournal: true), ct);
        }

        var now = clock.UtcNow;
        foreach (var row in wo.MaterialIssues.Where(i => i.ReversedAt is null)) row.ReversedAt = now;
    }

    public async Task ReverseJournalsAsync(WorkOrder wo, string memo, CancellationToken ct)
    {
        var posted = await journals.ListPostedOfSourceForUpdateAsync(WorkOrderRules.JournalSource, wo.Id, ct);
        foreach (var journal in posted)
            await posting.ReverseTodayAsync(journal, memo, ct);
    }

    /// <summary>Refuses to take a tracked item below zero unless the setting allows it. Items the stock service does not track are left out of on-hand and pass.</summary>
    private async Task EnsureAvailableAsync(WorkOrder wo, IReadOnlyList<MaterialNeed> needs, CancellationToken ct)
    {
        if (await settings.GetAsync<bool?>("manufacturing.allowNegativeStock", wo.LegalEntityId, ct) == true) return;
        var wanted = needs.GroupBy(n => n.ItemId).ToDictionary(g => g.Key, g => g.Sum(n => n.Quantity));
        var onHand = await stockPosting.GetOnHandAsync(wanted.Keys.ToList(), wo.WarehouseId, wo.LegalEntityId, ct);
        var shortages = wanted.Where(w => onHand.TryGetValue(w.Key, out var have) && have < w.Value)
            .Select(w => $"{needs.First(n => n.ItemId == w.Key).Code}: need {w.Value:0.####}, {onHand[w.Key]:0.####} on hand").ToList();
        if (shortages.Count > 0)
            throw new ConflictException($"Not enough stock in this warehouse. {string.Join("; ", shortages)}.");
    }
}
