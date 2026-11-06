using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Catalog;
using ERP.Domain.Finance;
using ERP.Modules.Inventory.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Inventory.Features;

public sealed class InventoryStockPostingService(
    IModuleAccess moduleAccess,
    IRepository<StockMovement> movements,
    IReadRepository<Warehouse> warehouses,
    IReadRepository<Item> items,
    IPostingService posting,
    IRepository<JournalEntry> journals,
    IUnitOfWork unitOfWork,
    ISettingsService settings) : IStockPostingService
{
    public async Task<bool> IsInventoryEnabledAsync(CancellationToken ct = default)
        => await moduleAccess.IsEnabledAsync(InventoryModule.Key, ct);

    public async Task<bool> IsWarehouseUsableAsync(Guid warehouseId, Guid legalEntityId, CancellationToken ct = default)
    {
        if (!await IsInventoryEnabledAsync(ct)) return true;
        return await warehouses.AnyAsync(w => w.Id == warehouseId && w.IsActive && w.LegalEntityId == legalEntityId, ct);
    }

    public async Task<Guid?> ReceiveStockAsync(StockReceiveRequest request, CancellationToken ct = default)
    {
        if (request.Quantity <= 0) return null;
        if (!await IsInventoryEnabledAsync(ct)) return null;

        var item = await items.FindAsync(request.ItemId, ct);
        if (item is null || item.Type != ItemType.Product) return null;

        var warehouse = await ResolveWarehouseAsync(request.WarehouseId, request.LegalEntityId, ct);
        if (warehouse is null)
            return request.RequireWarehouse ? throw new ConflictException(NoWarehouseMessage(item)) : null;

        return await unitOfWork.InTransactionAsync(async token =>
        {
            var m = new StockMovement
            {
                LegalEntityId = warehouse.LegalEntityId,
                WarehouseId = warehouse.Id,
                ItemId = request.ItemId,
                Date = request.Date,
                Kind = MovementKind.Receipt,
                Quantity = request.Quantity,
                UnitCost = request.UnitCost,
                Reference = request.Reference,
                Memo = request.Memo ?? "Stock receipt",
                ReturnOfReference = request.ReturnOfReference
            };

            var value = Math.Round(request.Quantity * request.UnitCost, 2);
            if (value > 0 && !request.SkipJournal)
            {
                Guid offsetAccount;
                if (request.OffsetAccountId.HasValue)
                {
                    offsetAccount = request.OffsetAccountId.Value;
                }
                else
                {
                    offsetAccount = await posting.AccountForRoleAsync(SystemRoles.InventoryClearing, token);
                }

                var invAccount = await posting.AccountForRoleAsync("inventory", token);
                var entry = await posting.PostAsync(new PostingRequest(
                    warehouse.LegalEntityId,
                    request.Date,
                    "inventory",
                    m.Id,
                    m.Memo,
                    request.Reference,
                    [
                        new PostingLine(invAccount, value, 0, m.Memo),
                        new PostingLine(offsetAccount, 0, value, m.Memo)
                    ]), token);

                m.JournalEntryId = entry.Id;
            }

            movements.Add(m);
            await movements.SaveChangesAsync(token);
            return m.Id;
        }, ct);
    }

    public async Task<Guid?> IssueStockAsync(StockIssueRequest request, CancellationToken ct = default)
    {
        if (request.Quantity <= 0) return null;
        if (!await IsInventoryEnabledAsync(ct)) return null;

        var item = await items.FindAsync(request.ItemId, ct);
        if (item is null || item.Type != ItemType.Product) return null;

        var warehouse = await ResolveWarehouseAsync(request.WarehouseId, request.LegalEntityId, ct);
        if (warehouse is null)
            return request.EnforceAvailable ? throw new ConflictException(NoWarehouseMessage(item)) : null;

        return await unitOfWork.InTransactionAsync(async token =>
        {
            var existing = await movements.Query(new Specification<StockMovement>(m => m.WarehouseId == warehouse.Id && m.ItemId == request.ItemId))
                .ToListAsync(token);
            if (request.EnforceAvailable) await EnsureAvailableAsync(request, warehouse, item, existing, token);
            var position = StockCosting.Replay(existing);

            var cost = position.AverageCost > 0 ? position.AverageCost : (item.PurchasePrice is > 0 ? item.PurchasePrice.Value : 0m);
            var m = new StockMovement
            {
                LegalEntityId = warehouse.LegalEntityId,
                WarehouseId = warehouse.Id,
                ItemId = request.ItemId,
                Date = request.Date,
                Kind = MovementKind.Issue,
                Quantity = -request.Quantity,
                UnitCost = cost,
                Reference = request.Reference,
                Memo = request.Memo ?? "Stock issued"
            };

            var value = Math.Round(request.Quantity * cost, 2);
            if (value > 0 && !request.SkipJournal)
            {
                var expense = request.ExpenseAccountId ?? await posting.AccountForRoleAsync("cogs", token);
                var invAccount = await posting.AccountForRoleAsync("inventory", token);

                var entry = await posting.PostAsync(new PostingRequest(
                    warehouse.LegalEntityId,
                    request.Date,
                    "inventory",
                    m.Id,
                    m.Memo,
                    request.Reference,
                    [
                        new PostingLine(expense, value, 0, m.Memo),
                        new PostingLine(invAccount, 0, value, m.Memo)
                    ]), token);

                m.JournalEntryId = entry.Id;
            }

            movements.Add(m);
            await movements.SaveChangesAsync(token);
            return m.Id;
        }, ct);
    }

    /// <summary>The document-driven negative guard; the movements passed in are this item's in this warehouse.</summary>
    private async Task EnsureAvailableAsync(StockIssueRequest request, Warehouse warehouse, Item item, List<StockMovement> existing, CancellationToken ct)
    {
        if (await settings.GetAsync<bool?>(InventoryModule.AllowNegativeStockSetting, warehouse.LegalEntityId, ct) ?? false) return;
        var available = existing.Where(m => m.Date <= request.Date).Sum(m => m.Quantity);
        if (available < request.Quantity)
            throw new ConflictException($"Only {available:0.####} of {item.Sku} is in stock at {warehouse.Code} on {request.Date:yyyy-MM-dd}; cannot issue {request.Quantity:0.####}.");
        var candidate = new StockMovement { Date = request.Date, Quantity = -request.Quantity };
        if (StockCosting.FirstShortfall(existing, candidate) is { } day)
            throw new ConflictException($"Issuing {request.Quantity:0.####} of {item.Sku} on {request.Date:yyyy-MM-dd} would leave stock negative on {day:yyyy-MM-dd}; later movements already use that stock.");
    }

    private static string NoWarehouseMessage(Item item) => $"There is no active warehouse in this legal entity to move {item.Sku} through. Add one or choose a warehouse.";

    public async Task<decimal> GetAverageCostAsync(Guid itemId, Guid? warehouseId = null, CancellationToken ct = default)
    {
        var item = await items.FindAsync(itemId, ct);
        if (item is null) return 0m;
        var wh = await ResolveWarehouseAsync(warehouseId, Guid.Empty, ct, strict: false);
        if (wh is null) return item.PurchasePrice ?? 0m;
        var pos = StockCosting.Replay(
            await movements.Query(new Specification<StockMovement>(m => m.WarehouseId == wh.Id && m.ItemId == itemId))
                .ToListAsync(ct));
        return pos.AverageCost > 0 ? pos.AverageCost : (item.PurchasePrice ?? 0m);
    }

    public async Task ReverseReceiptAsync(string reference, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reference)) return;
        if (!await IsInventoryEnabledAsync(ct)) return;

        var receiptMovements = await movements.Tracked(new Specification<StockMovement>(m => m.Reference == reference && m.Kind == MovementKind.Receipt))
            .ToListAsync(ct);

        if (receiptMovements.Count == 0) return;

        foreach (var m in receiptMovements)
        {
            var currentStock = (await movements.Query(new Specification<StockMovement>(sm => sm.WarehouseId == m.WarehouseId && sm.ItemId == m.ItemId)).ToListAsync(ct))
                .Sum(sm => sm.Quantity);

            if (currentStock - m.Quantity < 0)
            {
                throw new ConflictException($"Cannot delete goods receipt because item stock ({currentStock}) has already been consumed below received quantity ({m.Quantity}).");
            }

            if (m.JournalEntryId.HasValue)
            {
                var journal = await journals.Tracked(new Specification<JournalEntry>(j => j.Id == m.JournalEntryId.Value).Include(j => j.Lines))
                    .FirstOrDefaultAsync(ct);

                if (journal != null && journal.Status == JournalStatus.Posted)
                {
                    await posting.ReverseTodayAsync(journal, $"Reversal of GRN {reference}", ct);
                }
            }

            movements.Remove(m);
        }

        await movements.SaveChangesAsync(ct);
    }

    public async Task ReverseIssueAsync(string reference, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reference)) return;
        if (!await IsInventoryEnabledAsync(ct)) return;

        var issueMovements = await movements.Tracked(new Specification<StockMovement>(m => m.Reference == reference && m.Kind == MovementKind.Issue))
            .ToListAsync(ct);

        if (issueMovements.Count == 0) return;

        var cogsAccount = await posting.AccountForRoleAsync("cogs", ct);

        foreach (var m in issueMovements)
        {
            var qty = Math.Abs(m.Quantity);
            var returnCost = m.UnitCost;

            await ReceiveStockAsync(new StockReceiveRequest(
                m.LegalEntityId,
                m.ItemId,
                qty,
                returnCost,
                DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow),
                reference,
                $"Reversal of issue {reference}",
                m.WarehouseId,
                OffsetAccountId: cogsAccount,
                SkipJournal: !m.JournalEntryId.HasValue), ct);
        }
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetOnHandAsync(IReadOnlyCollection<Guid> itemIds, Guid? warehouseId, Guid legalEntityId, CancellationToken ct = default)
    {
        var result = new Dictionary<Guid, decimal>();
        if (itemIds.Count == 0 || !await IsInventoryEnabledAsync(ct)) return result;
        var warehouse = await ResolveWarehouseAsync(warehouseId, legalEntityId, ct, strict: false);
        if (warehouse is null) return result;

        var ids = itemIds.ToList();
        var tracked = await items.Query(new Specification<Item>(i => ids.Contains(i.Id) && i.Type == ItemType.Product)).Select(i => i.Id).ToListAsync(ct);
        foreach (var id in tracked) result[id] = 0m;
        // Added up here, not in SQL, because SQLite (tests) cannot sum decimals.
        var moves = await movements.Query(new Specification<StockMovement>(m => m.WarehouseId == warehouse.Id && tracked.Contains(m.ItemId))).Select(m => new { m.ItemId, m.Quantity }).ToListAsync(ct);
        foreach (var m in moves) result[m.ItemId] += m.Quantity;
        return result;
    }

    /// <summary>
    /// Newest issues give back first, each at the cost it left at, skipping units earlier returns of this reference already took,
    /// so the cost of sales reversed can never exceed what was charged. Refuses more than is still out.
    /// </summary>
    public async Task<Guid?> ReturnIssuedStockAsync(string issueReference, Guid itemId, decimal quantity, string returnReference, DateOnly date, CancellationToken ct = default)
    {
        if (quantity <= 0 || string.IsNullOrWhiteSpace(issueReference) || !await IsInventoryEnabledAsync(ct)) return null;
        var issues = (await movements.Query(new Specification<StockMovement>(m => m.Reference == issueReference && m.ItemId == itemId && m.Kind == MovementKind.Issue)).ToListAsync(ct))
            .OrderByDescending(m => m.Date).ThenByDescending(m => m.CreatedAt).ToList();
        if (issues.Count == 0) return null;
        var skip = (await movements.Query(new Specification<StockMovement>(m => m.ReturnOfReference == issueReference && m.ItemId == itemId && m.Kind == MovementKind.Receipt)).Select(m => m.Quantity).ToListAsync(ct)).Sum();
        var cogs = await posting.AccountForRoleAsync("cogs", ct);
        var left = quantity;
        Guid? last = null;
        foreach (var issue in issues)
        {
            var still = -issue.Quantity;
            var skipped = Math.Min(skip, still);
            skip -= skipped;
            var take = Math.Min(left, still - skipped);
            if (take <= 0) continue;
            last = await ReceiveStockAsync(new StockReceiveRequest(issue.LegalEntityId, itemId, take, issue.UnitCost, date, returnReference, $"Customer return {returnReference} of {issueReference}", issue.WarehouseId,
                OffsetAccountId: cogs, SkipJournal: !issue.JournalEntryId.HasValue, ReturnOfReference: issueReference), ct);
            left -= take;
            if (left <= 0) break;
        }
        if (left > 0) throw new ConflictException($"Only {quantity - left:0.####} of what {issueReference} issued is still out; cannot return {quantity:0.####}.");
        return last;
    }

    public async Task<decimal> GetMovementValueAsync(Guid movementId, CancellationToken ct = default)
    {
        var m = await movements.FindAsync(movementId, ct);
        return m is null ? 0m : Math.Round(Math.Abs(m.Quantity) * m.UnitCost, 2);
    }

    /// <summary>
    /// An explicit warehouse must exist, be active and belong to the legal entity: anything else is an error, never a silent switch (strict: false, for reads, returns null instead).
    /// With no warehouse given, the legal entity's first active warehouse by code; never a warehouse of another legal entity.
    /// </summary>
    private async Task<Warehouse?> ResolveWarehouseAsync(Guid? warehouseId, Guid legalEntityId, CancellationToken ct, bool strict = true)
    {
        if (warehouseId is { } id && id != Guid.Empty)
        {
            var wh = await warehouses.FindAsync(id, ct);
            if (wh is not null && wh.IsActive && (legalEntityId == Guid.Empty || wh.LegalEntityId == legalEntityId)) return wh;
            return strict ? throw new FieldValidationException("warehouseId", "The warehouse was not found, is not active, or belongs to another legal entity.") : null;
        }

        if (legalEntityId == Guid.Empty) return null;
        return await warehouses.Query(new Specification<Warehouse>(w => w.LegalEntityId == legalEntityId && w.IsActive)).OrderBy(w => w.Code).FirstOrDefaultAsync(ct);
    }
}
