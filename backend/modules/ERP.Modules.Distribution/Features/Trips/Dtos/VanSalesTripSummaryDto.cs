using System.Linq.Expressions;
using ERP.Modules.Distribution.Domain;

namespace ERP.Modules.Distribution.Features;

public record VanSalesTripSummaryDto(
    Guid Id,
    Guid LegalEntityId,
    string TripNumber,
    Guid RouteId,
    string RouteName,
    string DriverName,
    string VehicleNumber,
    DateOnly DepartureDate,
    TripStatus Status,
    decimal TotalSalesAmount,
    decimal TotalCollectionsAmount,
    int ItemCount,
    DateTimeOffset CreatedAt)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<VanSalesTrip, VanSalesTripSummaryDto>> Projection = t => new VanSalesTripSummaryDto(
        t.Id, t.LegalEntityId, t.TripNumber, t.RouteId, t.RouteName,
        t.DriverName, t.VehicleNumber, t.DepartureDate, t.Status,
        t.TotalSalesAmount, t.TotalCollectionsAmount, t.InventoryItems.Count, t.CreatedAt);
}
