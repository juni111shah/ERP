using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Retail.Domain;

namespace ERP.Modules.Retail.Features;

internal sealed class UpdateStoreHandler(IRepository<RetailStore> stores, StoreWarehouses warehouses) : ICommandHandler<UpdateStoreCommand, RetailStoreDto>
{
    public async Task<RetailStoreDto> Handle(UpdateStoreCommand request, CancellationToken cancellationToken)
    {
        // 1. Load it; a changed warehouse must be usable.
        var store = await stores.GetForUpdateAsync(request.Id, cancellationToken);
        if (request.WarehouseId != store.WarehouseId) await warehouses.EnsureWarehouseAsync(request.WarehouseId, store.LegalEntityId, cancellationToken);

        // 2. Apply the changes and save.
        store.Name = request.Name.Trim();
        store.WarehouseId = request.WarehouseId;
        store.StoreManagerName = request.StoreManagerName?.Trim();
        store.Phone = request.Phone?.Trim();
        store.Email = request.Email?.Trim();
        store.Address = request.Address?.Trim();
        store.IsActive = request.IsActive;
        store.CustomFields = request.CustomFields;

        await stores.SaveChangesAsync(cancellationToken);
        return RetailStoreDto.From(store);
    }
}
