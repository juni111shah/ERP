using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Modules.RealEstate.Domain;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

internal sealed class CreateUnitHandler(
    IRepository<Unit> units, IReadRepository<Property> properties, IReadRepository<UnitType> unitTypes, UnitBookkeeping bookkeeping, ICustomFieldService fields, IClock clock)
    : ICommandHandler<CreateUnitCommand, UnitDto>
{
    private DateOnly Today => DateOnly.FromDateTime(clock.UtcNow);

    public async Task<UnitDto> Handle(CreateUnitCommand cmd, CancellationToken ct)
    {
        // 1. The property and type must exist; the number is unique in the property.
        var i = cmd.Input;
        var property = await properties.FindAsync(i.PropertyId, ct) ?? throw new FieldValidationException("propertyId", "Property not found.");
        var type = await unitTypes.FindAsync(i.UnitTypeId, ct) ?? throw new FieldValidationException("unitTypeId", "Unit type not found.");
        var number = i.UnitNumber.Trim();
        if (await units.AnyAsync(u => u.PropertyId == property.Id && u.UnitNumber == number, ct))
            throw new ConflictException($"Unit {number} already exists in {property.Name}.");

        // 2. Create the unit with its custom fields and first asking rent.
        var unit = Unit.Create(Today);
        unit.PropertyId = property.Id;
        unit.LegalEntityId = property.LegalEntityId; // copied for entity scoping
        UnitBookkeeping.Map(i, unit, type, number);
        await fields.ApplyAsync(Docs.Unit, unit, i.CustomFields, isCreate: true, ct);
        units.Add(unit);
        if (unit.AskingRent > 0) bookkeeping.AddRate(unit, "Initial asking rent");

        // 3. The property counts it; save.
        await bookkeeping.GrowTotalUnitsAsync(property.Id, 1, ct);
        await units.SaveChangesAsync(ct);
        return UnitDto.From(unit, Today, await fields.ReadAsync(Docs.Unit, unit, ct));
    }
}
