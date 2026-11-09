using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.Retail.Domain;

namespace ERP.Modules.Retail.Features;

internal sealed class CreateStoreHandler(IRepository<RetailStore> stores, StoreWarehouses warehouses, ILegalEntityGuard guard) : ICommandHandler<CreateStoreCommand, RetailStoreDto>
{
    public async Task<RetailStoreDto> Handle(CreateStoreCommand request, CancellationToken cancellationToken)
    {
        // 1. The warehouse, when given, must be usable by the legal entity.
        await guard.RequireAsync(request.LegalEntityId, cancellationToken);
        await warehouses.EnsureWarehouseAsync(request.WarehouseId, request.LegalEntityId, cancellationToken);

        // 2. Build and save.
        var store = new RetailStore
        {
            LegalEntityId = request.LegalEntityId,
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            WarehouseId = request.WarehouseId,
            StoreManagerName = request.StoreManagerName?.Trim(),
            Phone = request.Phone?.Trim(),
            Email = request.Email?.Trim(),
            Address = request.Address?.Trim(),
            IsActive = request.IsActive,
            CustomFields = request.CustomFields
        };

        stores.Add(store);
        await stores.SaveChangesAsync(cancellationToken);
        return RetailStoreDto.From(store);
    }
}
