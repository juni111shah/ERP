using ERP.Modules.Distribution.Domain;

namespace ERP.Modules.Distribution.Features;

public record TripInventoryItemDto(
    Guid Id,
    Guid TripId,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    string Uom,
    decimal LoadedQuantity,
    decimal SoldQuantity,
    decimal ReturnedQuantity,
    decimal DamagedQuantity,
    decimal UnitPrice);

public record VanSalesTripDto(
    Guid Id,
    Guid LegalEntityId,
    string TripNumber,
    Guid RouteId,
    string RouteName,
    Guid DriverUserId,
    string DriverName,
    string VehicleNumber,
    Guid WarehouseId,
    DateOnly DepartureDate,
    DateOnly? ReturnDate,
    TripStatus Status,
    decimal TotalSalesAmount,
    decimal TotalCollectionsAmount,
    string? Notes,
    List<TripInventoryItemDto> InventoryItems,
    string? CustomFields,
    DateTimeOffset CreatedAt,
    Guid? InvoiceId = null,
    string? InvoiceNumber = null,
    IReadOnlyList<string>? Warnings = null)
{
    public static VanSalesTripDto From(VanSalesTrip t) => new(
        t.Id,
        t.LegalEntityId,
        t.TripNumber,
        t.RouteId,
        t.RouteName,
        t.DriverUserId,
        t.DriverName,
        t.VehicleNumber,
        t.WarehouseId,
        t.DepartureDate,
        t.ReturnDate,
        t.Status,
        t.TotalSalesAmount,
        t.TotalCollectionsAmount,
        t.Notes,
        t.InventoryItems.Select(i => new TripInventoryItemDto(
            i.Id,
            i.TripId,
            i.ItemId,
            i.ItemCode,
            i.ItemName,
            i.Uom,
            i.LoadedQuantity,
            i.SoldQuantity,
            i.ReturnedQuantity,
            i.DamagedQuantity,
            i.UnitPrice)).ToList(),
        t.CustomFields,
        t.CreatedAt,
        t.InvoiceId,
        t.InvoiceNumber);
}
