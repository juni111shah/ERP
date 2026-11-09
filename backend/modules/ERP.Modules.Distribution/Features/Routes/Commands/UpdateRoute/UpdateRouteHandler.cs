using ERP.Application.Common;
using ERP.Modules.Distribution.Domain;
using ERP.Modules.Distribution.Repositories;

namespace ERP.Modules.Distribution.Features;

internal sealed class UpdateRouteHandler(IDistributionRouteRepository routes, RouteReferences references) : ICommandHandler<UpdateRouteCommand, DistributionRouteDto>
{
    public async Task<DistributionRouteDto> Handle(UpdateRouteCommand request, CancellationToken cancellationToken)
    {
        // 1. Load it; a changed warehouse must be usable and every stop's customer must exist.
        var route = await routes.GetWithStopsForUpdateAsync(request.Id, cancellationToken);
        if (request.WarehouseId != route.WarehouseId) await references.EnsureWarehouseAsync(request.WarehouseId, route.LegalEntityId, cancellationToken);
        await references.EnsureStopCustomersAsync(request.Stops, cancellationToken);

        // 2. Apply the changes; stops, when sent, replace the current ones.
        route.Name = request.Name.Trim();
        route.Description = request.Description?.Trim();
        route.AssignedDriverUserId = request.AssignedDriverUserId;
        route.DriverName = request.DriverName?.Trim();
        route.VehicleNumber = request.VehicleNumber?.Trim();
        route.VehicleCapacityKg = request.VehicleCapacityKg;
        route.WarehouseId = request.WarehouseId;
        route.IsActive = request.IsActive;
        route.CustomFields = request.CustomFields;

        if (request.Stops != null)
        {
            route.Stops.Clear();
            foreach (var s in request.Stops)
            {
                route.Stops.Add(new DeliveryStop
                {
                    LegalEntityId = route.LegalEntityId,
                    RouteId = route.Id,
                    CustomerPartyId = s.CustomerPartyId,
                    CustomerName = s.CustomerName.Trim(),
                    SequenceOrder = s.SequenceOrder,
                    Address = s.Address?.Trim(),
                    ContactPhone = s.ContactPhone?.Trim(),
                    PreferredTimeWindow = s.PreferredTimeWindow?.Trim(),
                    SpecialInstructions = s.SpecialInstructions?.Trim(),
                    IsActive = true
                });
            }
        }

        // 3. Save.
        await routes.SaveChangesAsync(cancellationToken);
        return DistributionRouteDto.From(route);
    }
}
