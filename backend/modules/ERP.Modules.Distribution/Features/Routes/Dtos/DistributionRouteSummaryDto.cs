using System.Linq.Expressions;
using ERP.Modules.Distribution.Domain;

namespace ERP.Modules.Distribution.Features;

public record DistributionRouteSummaryDto(
    Guid Id,
    Guid LegalEntityId,
    string Code,
    string Name,
    string? DriverName,
    string? VehicleNumber,
    decimal? VehicleCapacityKg,
    Guid? WarehouseId,
    bool IsActive,
    int StopCount,
    DateTimeOffset CreatedAt)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<DistributionRoute, DistributionRouteSummaryDto>> Projection = r => new DistributionRouteSummaryDto(
        r.Id, r.LegalEntityId, r.Code, r.Name, r.DriverName, r.VehicleNumber,
        r.VehicleCapacityKg, r.WarehouseId, r.IsActive, r.Stops.Count, r.CreatedAt);
}
