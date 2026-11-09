using ERP.Application.Common;

namespace ERP.Modules.Distribution.Features;

/// <summary>Starts a van sales trip on a route with the stock loaded on the van.</summary>
public record CreateTripCommand(
    Guid LegalEntityId,
    Guid RouteId,
    Guid DriverUserId,
    string DriverName,
    string VehicleNumber,
    Guid WarehouseId,
    DateOnly DepartureDate,
    string? Notes,
    List<TripInventoryInput> Items,
    string? CustomFields) : ICommand<VanSalesTripDto>;
