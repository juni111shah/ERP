using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Parties;

namespace ERP.Modules.Distribution.Features;

/// <summary>Checks shared by creating and updating a route: its warehouse and the customers of its stops.</summary>
internal sealed class RouteReferences(IReferenceCheck references, IStockPostingService stock)
{
    public async Task EnsureWarehouseAsync(Guid? warehouseId, Guid legalEntityId, CancellationToken ct)
    {
        if (warehouseId is { } w && !await stock.IsWarehouseUsableAsync(w, legalEntityId, ct))
            throw new FieldValidationException("warehouseId", "Choose an active warehouse of this legal entity.");
    }

    public async Task EnsureStopCustomersAsync(List<DeliveryStopInput>? stops, CancellationToken ct)
    {
        if (stops is { Count: > 0 } && !await references.AllExistAsync<Party>(stops.Select(s => s.CustomerPartyId).ToList(), ct))
            throw new FieldValidationException("stops", "A stop's customer was not found.");
    }
}
