using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Modules.Inventory.Domain;
using ERP.Modules.Inventory.Repositories;

namespace ERP.Modules.Inventory.Features;

internal sealed class PostStockCountHandler(IStockCountRepository counts, IStockMovementRepository movements, IWarehouseRepository warehouses,
    IPostingService posting, IUnitOfWork unitOfWork, IClock clock, StockCountDtoBuilder dtos) : ICommandHandler<PostStockCountCommand, StockCountDto>
{
    public Task<StockCountDto> Handle(PostStockCountCommand cmd, CancellationToken ct) => unitOfWork.InTransactionAsync(async token =>
    {
        // 1. Load the count; posting a posted one again changes nothing, a cancelled or empty one cannot be posted.
        var count = await counts.GetWithLinesForUpdateAsync(cmd.Id, token);
        if (count.Status == StockCountStatus.Posted) return await dtos.ToDtoAsync(count, token); // idempotent
        if (count.Status == StockCountStatus.Cancelled) throw new ConflictException("A cancelled count cannot be posted.");
        if (count.Lines.All(l => l.CountedQuantity is null)) throw new ConflictException("Enter at least one counted quantity before posting.");

        // 2. The stock history of the counted items that are not booked yet.
        var wh = await warehouses.GetAsync(count.WarehouseId, token);
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var toPost = count.Lines.Where(l => l.CountedQuantity is not null && l.MovementId is null).ToList();
        var itemIds = toPost.Select(l => l.ItemId).ToList();
        var history = (await movements.ListForItemsInWarehouseAsync(count.WarehouseId, itemIds, token)).ToLookup(m => m.ItemId); // one query, not one per line

        // 3. One adjustment per difference at the average cost, posted when it has a value.
        foreach (var line in toPost)
        {
            var position = StockCosting.Replay(history[line.ItemId].ToList());
            var delta = line.CountedQuantity!.Value - position.Quantity;
            if (delta == 0) continue;
            var cost = position.AverageCost;
            var movement = new StockMovement
            {
                LegalEntityId = wh.LegalEntityId, WarehouseId = wh.Id, ItemId = line.ItemId, Date = count.CountDate > today ? today : count.CountDate, Kind = MovementKind.Adjustment, Quantity = delta,
                UnitCost = cost, Reference = count.Number, Memo = $"Stock count {count.Number}",
            };
            var value = Math.Round(Math.Abs(delta) * cost, 2);
            if (value > 0)
            {
                var inv = await posting.AccountForRoleAsync("inventory", token);
                var offset = await posting.AccountForRoleAsync("cogs", token);
                var lines = delta > 0 ? new[] { new PostingLine(inv, value, 0), new PostingLine(offset, 0, value) } : [new PostingLine(offset, value, 0), new PostingLine(inv, 0, value)];
                movement.JournalEntryId = (await posting.PostAsync(new PostingRequest(wh.LegalEntityId, movement.Date, "inventory", movement.Id, movement.Memo, count.Number, lines), token)).Id;
            }
            movements.Add(movement);
            line.MovementId = movement.Id;
        }

        // 4. Mark the count posted and save.
        count.Status = StockCountStatus.Posted;
        count.PostedAt = clock.UtcNow;
        await counts.SaveChangesAsync(token);
        return await dtos.ToDtoAsync(count, token);
    }, ct);
}
