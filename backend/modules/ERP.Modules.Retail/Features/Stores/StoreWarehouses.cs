using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Modules.Retail.Features;

/// <summary>The warehouse check shared by creating and updating a store.</summary>
internal sealed class StoreWarehouses(IStockPostingService stock)
{
    public async Task EnsureWarehouseAsync(Guid? warehouseId, Guid legalEntityId, CancellationToken ct)
    {
        if (warehouseId is { } w && !await stock.IsWarehouseUsableAsync(w, legalEntityId, ct))
            throw new FieldValidationException("warehouseId", "Choose an active warehouse of this legal entity.");
    }
}
