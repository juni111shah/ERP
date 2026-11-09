using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Modules.Inventory.Domain;
using ERP.Modules.Inventory.Repositories;

namespace ERP.Modules.Inventory.Features;

internal sealed class ReceiveStockHandler(IStockMovementRepository movements, StockMovementChecks checks, IPostingService posting, IUnitOfWork unitOfWork) : ICommandHandler<ReceiveStockCommand, MovementDto>
{
    public Task<MovementDto> Handle(ReceiveStockCommand cmd, CancellationToken ct) => unitOfWork.InTransactionAsync(async token =>
    {
        // 1. Check the warehouse, item and offset account.
        var i = cmd.Input;
        var (wh, _) = await checks.RequireAsync(i.WarehouseId, i.ItemId, token);
        await checks.RequireAccountAsync(i.OffsetAccountId, "offsetAccountId", token);

        // 2. The receipt; a valued one is posted to inventory against the offset account.
        var m = StockMovementChecks.New(wh, i.ItemId, i.Date, MovementKind.Receipt, i.Quantity, i.UnitCost, i.Reference, i.Memo);
        var value = Math.Round(i.Quantity * i.UnitCost, 2);
        if (value > 0)
            m.JournalEntryId = (await posting.PostAsync(new PostingRequest(wh.LegalEntityId, i.Date, "inventory", m.Id, i.Memo ?? "Stock received", i.Reference,
                [new PostingLine(await posting.AccountForRoleAsync("inventory", token), value, 0, "Stock received"), new PostingLine(i.OffsetAccountId, 0, value, "Stock received")]), token)).Id;

        // 3. Save.
        movements.Add(m);
        await movements.SaveChangesAsync(token);
        return MovementDto.From(m);
    }, ct);
}
