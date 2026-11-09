using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Repositories;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

internal sealed class BulkCreateUnitsHandler(
    IUnitRepository units, IReadRepository<Property> properties, IReadRepository<UnitType> unitTypes, UnitBookkeeping bookkeeping, IClock clock)
    : ICommandHandler<BulkCreateUnitsCommand, IReadOnlyList<UnitDto>>
{
    private DateOnly Today => DateOnly.FromDateTime(clock.UtcNow);

    public async Task<IReadOnlyList<UnitDto>> Handle(BulkCreateUnitsCommand cmd, CancellationToken ct)
    {
        // 1. The property and type must exist.
        var i = cmd.Input;
        var property = await properties.FindAsync(i.PropertyId, ct) ?? throw new FieldValidationException("propertyId", "Property not found.");
        var type = await unitTypes.FindAsync(i.UnitTypeId, ct) ?? throw new FieldValidationException("unitTypeId", "Unit type not found.");

        // 2. Unit numbers: floor and a 2-digit position ("1406" = floor 14, unit 06); ground floor 0 gives "001".."008".
        var numbers = new List<(string Number, int Floor)>();
        for (var floor = i.FromFloor; floor <= i.ToFloor; floor++)
            for (var n = 1; n <= i.UnitsPerFloor; n++)
                numbers.Add(($"{i.Prefix?.Trim()}{floor}{n:00}", floor));

        // 3. None of them may exist yet.
        var wanted = numbers.Select(x => x.Number).ToList();
        var existing = await units.ListExistingNumbersAsync(property.Id, wanted, ct);
        if (existing.Count > 0) throw new ConflictException($"These units already exist: {string.Join(", ", existing.Take(10))}{(existing.Count > 10 ? "..." : "")}.");

        // 4. Create them with their first asking rent.
        var created = new List<Unit>();
        foreach (var (number, floor) in numbers)
        {
            var unit = Unit.Create(Today);
            unit.PropertyId = property.Id;
            unit.LegalEntityId = property.LegalEntityId;
            unit.UnitNumber = number; unit.UnitTypeId = type.Id; unit.Usage = type.DefaultUsage; unit.Bedrooms = type.DefaultBedrooms;
            unit.Floor = floor.ToString(); unit.AreaSqft = i.AreaSqft; unit.AskingRent = i.AskingRent;
            units.Add(unit);
            if (unit.AskingRent > 0) bookkeeping.AddRate(unit, "Initial asking rent");
            created.Add(unit);
        }

        // 5. The property counts them; save.
        await bookkeeping.GrowTotalUnitsAsync(property.Id, created.Count, ct);
        await units.SaveChangesAsync(ct);
        return created.Select(u => UnitDto.From(u, Today, new JsonObject())).ToList();
    }
}
