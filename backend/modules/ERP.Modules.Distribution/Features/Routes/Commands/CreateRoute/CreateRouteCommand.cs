using ERP.Application.Common;

namespace ERP.Modules.Distribution.Features;

/// <summary>Creates a delivery route with its stops.</summary>
public record CreateRouteCommand(
    Guid LegalEntityId,
    string Code,
    string Name,
    string? Description,
    Guid? AssignedDriverUserId,
    string? DriverName,
    string? VehicleNumber,
    decimal? VehicleCapacityKg,
    Guid? WarehouseId,
    bool IsActive,
    List<DeliveryStopInput>? Stops,
    string? CustomFields) : ICommand<DistributionRouteDto>;
