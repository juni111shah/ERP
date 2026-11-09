using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Repositories;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

internal sealed class GetPropertyHandler(IReadRepository<Property> properties, IReadRepository<Unit> units, ILeaseRepository leases, ICustomFieldService fields)
    : IQueryHandler<GetPropertyQuery, PropertySummaryDto>
{
    public async Task<PropertySummaryDto> Handle(GetPropertyQuery q, CancellationToken ct)
    {
        // 1. The property, its registered and occupied units and the rent of its occupying leases.
        var property = await properties.GetAsync(q.Id, ct);
        var registered = await units.CountAsync(u => u.PropertyId == property.Id, ct);
        var occupied = await units.CountAsync(u => u.PropertyId == property.Id && u.Status == UnitStatus.Occupied, ct);
        var contracted = await leases.ContractedRentOfPropertyAsync(property.Id, ct);

        // 2. The KPI cards.
        var occupancy = registered == 0 ? 0m : Math.Round(occupied * 100m / registered, 1);
        var yield = property.IndicativeValue > 0 ? Math.Round(contracted * 100m / property.IndicativeValue, 2) : 0m;
        return new(PropertyDto.From(property, await fields.ReadAsync(Docs.Property, property, ct)), registered, occupied, occupancy, contracted, yield, registered < property.TotalUnits);
    }
}
