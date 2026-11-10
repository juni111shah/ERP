using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.Distribution.Domain;
using ERP.Modules.Distribution.Repositories;

namespace ERP.Modules.Distribution.Features;

internal sealed class CreateTripHandler(
    IVanSalesTripRepository trips,
    IDistributionRouteRepository routes,
    IItemRepository items,
    INumberingService numbering,
    IStockPostingService stockPosting,
    ILegalEntityGuard guard)
    : ICommandHandler<CreateTripCommand, VanSalesTripDto>
{
    public async Task<VanSalesTripDto> Handle(CreateTripCommand request, CancellationToken cancellationToken)
    {
        // 1. The route and warehouse must be of this legal entity, and every loaded item must exist.
        await guard.RequireAsync(request.LegalEntityId, cancellationToken);
        var route = await routes.FindAsync(request.RouteId, cancellationToken);
        if (route is null || route.LegalEntityId != request.LegalEntityId) throw new FieldValidationException("routeId", "Route not found in this legal entity.");
        if (!await stockPosting.IsWarehouseUsableAsync(request.WarehouseId, request.LegalEntityId, cancellationToken)) throw new FieldValidationException("warehouseId", "The warehouse was not found, is not active, or belongs to another legal entity.");
        var routeName = route.Name;

        var itemIds = request.Items.Select(i => i.ItemId).ToList();
        var catalog = await items.GetSkuNamesAsync(itemIds, cancellationToken);
        if (catalog.Count != itemIds.Distinct().Count()) throw new FieldValidationException("items", "An item was not found.");

        // 2. Number it and build the trip with what was loaded.
        var tripNumber = await numbering.NextAsync("VanSalesTrip", request.LegalEntityId, request.DepartureDate, cancellationToken);

        var trip = new VanSalesTrip
        {
            LegalEntityId = request.LegalEntityId,
            TripNumber = tripNumber,
            RouteId = request.RouteId,
            RouteName = routeName,
            DriverUserId = request.DriverUserId,
            DriverName = request.DriverName.Trim(),
            VehicleNumber = request.VehicleNumber.Trim(),
            WarehouseId = request.WarehouseId,
            DepartureDate = request.DepartureDate,
            Status = TripStatus.InProgress,
            TotalSalesAmount = 0,
            TotalCollectionsAmount = 0,
            Notes = request.Notes?.Trim(),
            CustomFields = request.CustomFields
        };

        foreach (var itm in request.Items)
        {
            trip.InventoryItems.Add(new TripInventoryItem
            {
                LegalEntityId = trip.LegalEntityId,
                TripId = trip.Id,
                ItemId = itm.ItemId,
                ItemCode = catalog[itm.ItemId].Sku, // from the catalog, not from the request
                ItemName = catalog[itm.ItemId].Name,
                Uom = string.IsNullOrWhiteSpace(itm.Uom) ? "PCS" : itm.Uom.Trim(),
                LoadedQuantity = itm.LoadedQuantity,
                SoldQuantity = 0,
                ReturnedQuantity = 0,
                DamagedQuantity = 0,
                UnitPrice = itm.UnitPrice
            });
        }

        // 3. Save.
        trips.Add(trip);
        await trips.SaveChangesAsync(cancellationToken);
        return VanSalesTripDto.From(trip);
    }
}
