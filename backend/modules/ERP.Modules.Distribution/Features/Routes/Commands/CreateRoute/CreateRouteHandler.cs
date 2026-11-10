using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.Distribution.Domain;
using ERP.Modules.Distribution.Repositories;

namespace ERP.Modules.Distribution.Features;

internal sealed class CreateRouteHandler(IDistributionRouteRepository routes, RouteReferences references, ILegalEntityGuard guard) : ICommandHandler<CreateRouteCommand, DistributionRouteDto>
{
    public async Task<DistributionRouteDto> Handle(CreateRouteCommand request, CancellationToken cancellationToken)
    {
        // 1. The warehouse must be usable by the legal entity and every stop's customer must exist.
        await guard.RequireAsync(request.LegalEntityId, cancellationToken);
        await references.EnsureWarehouseAsync(request.WarehouseId, request.LegalEntityId, cancellationToken);
        await references.EnsureStopCustomersAsync(request.Stops, cancellationToken);

        // 2. Build the route with its stops.
        var route = new DistributionRoute
        {
            LegalEntityId = request.LegalEntityId,
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            AssignedDriverUserId = request.AssignedDriverUserId,
            DriverName = request.DriverName?.Trim(),
            VehicleNumber = request.VehicleNumber?.Trim(),
            VehicleCapacityKg = request.VehicleCapacityKg,
            WarehouseId = request.WarehouseId,
            IsActive = request.IsActive,
            CustomFields = request.CustomFields
        };

        if (request.Stops != null && request.Stops.Count > 0)
        {
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
        routes.Add(route);
        await routes.SaveChangesAsync(cancellationToken);
        return DistributionRouteDto.From(route);
    }
}
