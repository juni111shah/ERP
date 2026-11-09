using ERP.Modules.Distribution.Domain;

namespace ERP.Modules.Distribution.Features;

public record DeliveryStopDto(
    Guid Id,
    Guid RouteId,
    Guid CustomerPartyId,
    string CustomerName,
    int SequenceOrder,
    string? Address,
    string? ContactPhone,
    string? PreferredTimeWindow,
    string? SpecialInstructions,
    bool IsActive);

public record DistributionRouteDto(
    Guid Id,
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
    int StopCount,
    List<DeliveryStopDto> Stops,
    string? CustomFields,
    DateTimeOffset CreatedAt)
{
    public static DistributionRouteDto From(DistributionRoute r) => new(
        r.Id,
        r.LegalEntityId,
        r.Code,
        r.Name,
        r.Description,
        r.AssignedDriverUserId,
        r.DriverName,
        r.VehicleNumber,
        r.VehicleCapacityKg,
        r.WarehouseId,
        r.IsActive,
        r.Stops.Count,
        r.Stops.OrderBy(s => s.SequenceOrder).Select(s => new DeliveryStopDto(
            s.Id,
            s.RouteId,
            s.CustomerPartyId,
            s.CustomerName,
            s.SequenceOrder,
            s.Address,
            s.ContactPhone,
            s.PreferredTimeWindow,
            s.SpecialInstructions,
            s.IsActive)).ToList(),
        r.CustomFields,
        r.CreatedAt);
}
