using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.Inventory.Repositories;

namespace ERP.Modules.Inventory;

internal sealed class CreateWarehouseHandler(IWarehouseRepository warehouses, ILegalEntityGuard legalEntityGuard) : ICommandHandler<CreateWarehouseCommand, WarehouseDto>
{
    public async Task<WarehouseDto> Handle(CreateWarehouseCommand cmd, CancellationToken ct)
    {
        // 1. The caller must have access to the legal entity, and the code must be free.
        var i = cmd.Input;
        await legalEntityGuard.RequireAsync(i.LegalEntityId, ct);
        var code = i.Code.Trim();
        if (await warehouses.AnyAsync(w => w.Code == code, ct)) throw new ConflictException($"A warehouse with code '{code}' already exists.");

        // 2. Create and save.
        var warehouse = new Warehouse { Code = code, Name = i.Name.Trim(), LegalEntityId = i.LegalEntityId, Address = i.Address };
        warehouses.Add(warehouse);
        await warehouses.SaveChangesAsync(ct);
        return new(warehouse.Id, warehouse.Code, warehouse.Name, warehouse.LegalEntityId, warehouse.Address, warehouse.IsActive);
    }
}
