using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Modules.RealEstate.Domain;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

internal sealed class UpdateUnitHandler(IRepository<Unit> units, IReadRepository<UnitType> unitTypes, UnitBookkeeping bookkeeping, ICustomFieldService fields, IClock clock)
    : ICommandHandler<UpdateUnitCommand, UnitDto>
{
    private DateOnly Today => DateOnly.FromDateTime(clock.UtcNow);

    public async Task<UnitDto> Handle(UpdateUnitCommand cmd, CancellationToken ct)
    {
        // 1. Load it; it stays in its property, the type must exist and the number stays unique.
        var i = cmd.Input;
        var unit = await units.GetForUpdateAsync(cmd.Id, ct);
        if (i.PropertyId != unit.PropertyId) throw new FieldValidationException("propertyId", "A unit cannot move to another property.");
        var type = await unitTypes.FindAsync(i.UnitTypeId, ct) ?? throw new FieldValidationException("unitTypeId", "Unit type not found.");
        var number = i.UnitNumber.Trim();
        if (number != unit.UnitNumber && await units.AnyAsync(u => u.PropertyId == unit.PropertyId && u.UnitNumber == number, ct))
            throw new ConflictException($"Unit {number} already exists in this property.");

        // 2. Apply the form and the custom fields; a new asking rent goes into the history.
        var oldRent = unit.AskingRent;
        UnitBookkeeping.Map(i, unit, type, number);
        await fields.ApplyAsync(Docs.Unit, unit, i.CustomFields, isCreate: false, ct);
        if (unit.AskingRent != oldRent) await bookkeeping.RecordRateAsync(unit, ct);
        await units.SaveChangesAsync(ct);
        return UnitDto.From(unit, Today, await fields.ReadAsync(Docs.Unit, unit, ct));
    }
}
