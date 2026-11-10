using ERP.Application.Common;
using ERP.Modules.Inventory.Domain;
using ERP.Modules.Inventory.Repositories;

namespace ERP.Modules.Inventory.Features;

internal sealed class CreateStockCountHandler(IStockCountRepository counts, IStockMovementRepository movements, IWarehouseRepository warehouses, StockCountDtoBuilder dtos)
    : ICommandHandler<CreateStockCountCommand, StockCountDto>
{
    public async Task<StockCountDto> Handle(CreateStockCountCommand cmd, CancellationToken ct)
    {
        // 1. An active warehouse; the count takes the next CNT number.
        var wh = await warehouses.FindAsync(cmd.Input.WarehouseId, ct) ?? throw new FieldValidationException("warehouseId", "Warehouse not found.");
        if (!wh.IsActive) throw new ConflictException($"Warehouse {wh.Code} is not active.");
        var number = $"CNT-{(await counts.CountAsync(null, ct)) + 1:D5}";
        var count = new StockCount { LegalEntityId = wh.LegalEntityId, WarehouseId = wh.Id, CountDate = cmd.Input.CountDate, Memo = cmd.Input.Memo, Number = number };

        // 2. One line per item held there, with its system quantity.
        var positions = (await movements.ListForWarehouseAsync(wh.Id, ct)).GroupBy(m => m.ItemId).Select(g => (Item: g.Key, Pos: StockCosting.Replay(g))).Where(x => x.Pos.Quantity != 0);
        count.Lines.AddRange(positions.Select(x => new StockCountLine { StockCountId = count.Id, ItemId = x.Item, SystemQuantity = x.Pos.Quantity }));

        // 3. Save.
        counts.Add(count);
        await counts.SaveChangesAsync(ct);
        return await dtos.ToDtoAsync(count, ct);
    }
}
