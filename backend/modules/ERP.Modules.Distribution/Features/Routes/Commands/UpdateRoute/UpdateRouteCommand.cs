using ERP.Application.Common;

namespace ERP.Modules.Distribution.Features;

/// <summary>Updates a delivery route; stops, when given, replace the current ones.</summary>
public record UpdateRouteCommand(
    Guid Id,
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
