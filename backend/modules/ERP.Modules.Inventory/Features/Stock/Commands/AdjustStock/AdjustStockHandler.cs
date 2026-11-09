using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Modules.Inventory.Domain;
using ERP.Modules.Inventory.Repositories;

namespace ERP.Modules.Inventory.Features;

internal sealed class AdjustStockHandler(IStockMovementRepository movements, StockMovementChecks checks, IPostingService posting, IUnitOfWork unitOfWork) : ICommandHandler<AdjustStockCommand, MovementDto>
{
    public Task<MovementDto> Handle(AdjustStockCommand cmd, CancellationToken ct) => unitOfWork.InTransactionAsync(async token =>
    {
        // 1. Check the warehouse and item; a reduction needs the stock on the date.
        var i = cmd.Input;
        var (wh, _) = await checks.RequireAsync(i.WarehouseId, i.ItemId, token);
        var position = await checks.PositionAsync(i.WarehouseId, i.ItemId, token, i.Date);
        if (i.QuantityDelta < 0 && position.Quantity < -i.QuantityDelta) throw new ConflictException($"Only {position.Quantity:0.####} in stock; cannot reduce by {-i.QuantityDelta:0.####}.");

        // 2. The adjustment: an increase at the given cost, a reduction at the average cost (and not starving later movements).
        var cost = i.QuantityDelta > 0 ? i.UnitCost : position.AverageCost;
        var m = StockMovementChecks.New(wh, i.ItemId, i.Date, MovementKind.Adjustment, i.QuantityDelta, cost, i.Reference, i.Memo ?? "Stock adjustment");
        if (i.QuantityDelta < 0) await checks.EnsureNoShortfallAsync(m, token);

        // 3. A valued adjustment is posted between inventory and the offset account.
        var value = Math.Round(Math.Abs(i.QuantityDelta) * cost, 2);
        if (value > 0)
        {
            var offset = i.OffsetAccountId ?? await posting.AccountForRoleAsync("cogs", token);
            await checks.RequireAccountAsync(offset, "offsetAccountId", token);
            var inv = await posting.AccountForRoleAsync("inventory", token);
            var lines = i.QuantityDelta > 0 ? new[] { new PostingLine(inv, value, 0), new PostingLine(offset, 0, value) } : [new PostingLine(offset, value, 0), new PostingLine(inv, 0, value)];
            m.JournalEntryId = (await posting.PostAsync(new PostingRequest(wh.LegalEntityId, i.Date, "inventory", m.Id, m.Memo, i.Reference, lines), token)).Id;
        }

        // 4. Save.
        movements.Add(m);
        await movements.SaveChangesAsync(token);
        return MovementDto.From(m);
    }, ct);
}
