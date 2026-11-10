using ERP.Application.Abstractions;
using ERP.Modules.RealEstate.Domain;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

/// <summary>Builds inspection DTOs with the property name and unit number (shared by the inspection handlers).</summary>
internal sealed class InspectionDtoFactory(IReadRepository<Property> properties, IReadRepository<Unit> units)
{
    public async Task<InspectionDto> BuildAsync(Inspection i, CancellationToken ct)
    {
        var property = await properties.GetAsync(i.PropertyId, ct);
        var unitNumber = i.UnitId is { } u ? (await units.FindAsync(u, ct))?.UnitNumber : null;
        return new(i.Id, i.PropertyId, property.Name, i.UnitId, unitNumber, i.LeaseId, i.Kind, i.InspectionDate, i.Inspector, i.Status, i.Notes, i.DamageCost,
            i.Items.Select(t => new InspectionItemDto(t.Id, t.Area, t.Condition, t.Notes, t.RepairCost, t.WorkOrderId)).ToList());
    }
}
