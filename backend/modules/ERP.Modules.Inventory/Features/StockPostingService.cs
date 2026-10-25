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
    IUnitOfWork unitOfWork) : IStockPostingService
{
    public async Task<bool> IsInventoryEnabledAsync(CancellationToken ct = default)
        => await moduleAccess.IsEnabledAsync(InventoryModule.Key, ct);

    public async Task<Guid?> ReceiveStockAsync(StockReceiveRequest request, CancellationToken ct = default)
    {
        if (request.Quantity <= 0) return null;
        if (!await IsInventoryEnabledAsync(ct)) return null;

        var item = await items.FindAsync(request.ItemId, ct);
        if (item is null || item.Type != ItemType.Product) return null;

        var warehouse = await ResolveWarehouseAsync(request.WarehouseId, request.LegalEntityId, ct);
        if (warehouse is null) return null;

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
                Memo = request.Memo ?? "Stock receipt"
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
        if (warehouse is null) return null;

        return await unitOfWork.InTransactionAsync(async token =>
        {
            var position = StockCosting.Replay(
                await movements.Query(new Specification<StockMovement>(m => m.WarehouseId == warehouse.Id && m.ItemId == request.ItemId))
                    .ToListAsync(token));

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

    public async Task<decimal> GetAverageCostAsync(Guid itemId, Guid? warehouseId = null, CancellationToken ct = default)
    {
        var item = await items.FindAsync(itemId, ct);
        if (item is null) return 0m;
        var wh = await ResolveWarehouseAsync(warehouseId, Guid.Empty, ct);
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

    private async Task<Warehouse?> ResolveWarehouseAsync(Guid? warehouseId, Guid legalEntityId, CancellationToken ct)
    {
        if (warehouseId.HasValue)
        {
            var wh = await warehouses.FindAsync(warehouseId.Value, ct);
            if (wh != null && wh.IsActive && (legalEntityId == Guid.Empty || wh.LegalEntityId == legalEntityId)) return wh;
        }

        if (legalEntityId != Guid.Empty)
        {
            var entityWh = await warehouses.Query(new Specification<Warehouse>(w => w.LegalEntityId == legalEntityId && w.IsActive))
                .FirstOrDefaultAsync(ct);
            if (entityWh != null) return entityWh;
        }

        return await warehouses.Query(new Specification<Warehouse>(w => w.IsActive)).FirstOrDefaultAsync(ct);
    }
}
