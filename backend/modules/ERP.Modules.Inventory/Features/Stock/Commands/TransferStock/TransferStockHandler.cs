using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Inventory.Domain;
using ERP.Modules.Inventory.Repositories;

namespace ERP.Modules.Inventory.Features;

internal sealed class TransferStockHandler(IStockMovementRepository movements, StockMovementChecks checks, IUnitOfWork unitOfWork) : ICommandHandler<TransferStockCommand, IReadOnlyList<MovementDto>>
{
    public Task<IReadOnlyList<MovementDto>> Handle(TransferStockCommand cmd, CancellationToken ct) => unitOfWork.InTransactionAsync<IReadOnlyList<MovementDto>>(async token =>
    {
        // 1. Both warehouses of one legal entity, and the stock at the source on the date.
        var i = cmd.Input;
        var (from, _) = await checks.RequireAsync(i.FromWarehouseId, i.ItemId, token);
        var (to, _) = await checks.RequireAsync(i.ToWarehouseId, i.ItemId, token);
        if (from.LegalEntityId != to.LegalEntityId) throw new ConflictException("Transfers are between warehouses of the same legal entity.");
        var position = await checks.PositionAsync(i.FromWarehouseId, i.ItemId, token, i.Date);
        if (position.Quantity < i.Quantity) throw new ConflictException($"Only {position.Quantity:0.####} in stock at the source warehouse.");

        // 2. The two sides of the transfer at the source's average cost; the out side must not starve later movements.
        var transfer = Guid.NewGuid();
        var memo = i.Memo ?? $"Transfer {from.Code} → {to.Code}";
        var outM = StockMovementChecks.New(from, i.ItemId, i.Date, MovementKind.TransferOut, -i.Quantity, position.AverageCost, i.Reference, memo); outM.TransferId = transfer;
        await checks.EnsureNoShortfallAsync(outM, token);
        var inM = StockMovementChecks.New(to, i.ItemId, i.Date, MovementKind.TransferIn, i.Quantity, position.AverageCost, i.Reference, memo); inM.TransferId = transfer;

        // 3. Save.
        movements.AddRange([outM, inM]);
        await movements.SaveChangesAsync(token);
        return [MovementDto.From(outM), MovementDto.From(inM)];
    }, ct);
}
