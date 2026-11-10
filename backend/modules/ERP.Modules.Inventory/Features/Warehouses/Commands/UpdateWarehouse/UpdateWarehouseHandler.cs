using ERP.Application.Common;
using ERP.Modules.Inventory.Features;
using ERP.Modules.Inventory.Repositories;

namespace ERP.Modules.Inventory;

internal sealed class UpdateWarehouseHandler(IWarehouseRepository warehouses, IStockMovementRepository movements) : ICommandHandler<UpdateWarehouseCommand, WarehouseDto>
{
    public async Task<WarehouseDto> Handle(UpdateWarehouseCommand cmd, CancellationToken ct)
    {
        // 1. Load the warehouse; it can be deactivated only once it holds no stock.
        var w = await warehouses.GetForUpdateAsync(cmd.Id, ct);
        if (w.IsActive && !cmd.Input.IsActive && await HoldsStockAsync(w.Id, ct))
            throw new ConflictException($"Warehouse {w.Code} still holds stock. Move or issue it first.");

        // 2. Apply the changes and save.
        w.Name = cmd.Input.Name.Trim(); w.Address = cmd.Input.Address; w.IsActive = cmd.Input.IsActive;
        await warehouses.SaveChangesAsync(ct);
        return new(w.Id, w.Code, w.Name, w.LegalEntityId, w.Address, w.IsActive);
    }

    private async Task<bool> HoldsStockAsync(Guid warehouseId, CancellationToken ct)
    {
        var all = await movements.ListForWarehouseAsync(warehouseId, ct);
        return all.GroupBy(m => m.ItemId).Any(g => StockCosting.Replay(g).Quantity != 0);
    }
}
