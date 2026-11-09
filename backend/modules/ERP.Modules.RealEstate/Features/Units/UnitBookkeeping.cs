using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Repositories;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

/// <summary>Keeps the rent history of units and the unit count of their property in step (shared by create, update and bulk create).</summary>
internal sealed class UnitBookkeeping(IReadRepository<Unit> units, IRepository<Property> properties, IUnitRentRateRepository rates, IClock clock)
{
    private DateOnly Today => DateOnly.FromDateTime(clock.UtcNow);

    /// <summary>Keeps the invariant "total lettable units >= registered units" (spec RE-PROP-04) when units are added.</summary>
    public async Task GrowTotalUnitsAsync(Guid propertyId, int adding, CancellationToken ct)
    {
        var registered = await units.CountAsync(u => u.PropertyId == propertyId, ct) + adding;
        var property = await properties.GetForUpdateAsync(propertyId, ct);
        if (registered > property.TotalUnits) property.TotalUnits = registered;
    }

    public static void Map(UnitInput i, Unit unit, UnitType type, string number)
    {
        unit.UnitNumber = number; unit.UnitTypeId = type.Id;
        unit.Usage = i.Usage ?? type.DefaultUsage; // usage drives the default VAT treatment of leases
        unit.Floor = i.Floor; unit.Bedrooms = i.Bedrooms ?? type.DefaultBedrooms; unit.AreaSqft = i.AreaSqft; unit.AskingRent = i.AskingRent;
        unit.Notes = i.Notes;
    }

    public void AddRate(Unit unit, string reason)
        => rates.Add(new UnitRentRate { UnitId = unit.Id, LegalEntityId = unit.LegalEntityId, EffectiveFrom = Today, AnnualRent = unit.AskingRent, Reason = reason });

    /// <summary>One rate per day: a second change on the same day replaces the first (unique per unit and date).</summary>
    public async Task RecordRateAsync(Unit unit, CancellationToken ct)
    {
        var today = Today;
        var sameDay = await rates.FindForDayForUpdateAsync(unit.Id, today, ct);
        if (sameDay is null) AddRate(unit, "Asking rent changed");
        else sameDay.AnnualRent = unit.AskingRent;
    }
}
