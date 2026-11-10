using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

/// <summary>Checks what an inspection form refers to and copies it onto the inspection (shared by create and update).</summary>
internal sealed class InspectionInputRules(IReadRepository<Property> properties, IReadRepository<Unit> units, IReadRepository<Lease> leases)
{
    public async Task MapAsync(InspectionInput i, Inspection x, CancellationToken ct)
    {
        var property = await properties.FindAsync(i.PropertyId, ct) ?? throw new FieldValidationException("propertyId", "Property not found.");
        if (i.UnitId is { } unitId && !await units.AnyAsync(u => u.Id == unitId && u.PropertyId == i.PropertyId, ct)) throw new FieldValidationException("unitId", "That unit is not in this property.");
        if (i.LeaseId is { } leaseId && !await leases.AnyAsync(l => l.Id == leaseId && l.PropertyId == i.PropertyId, ct)) throw new FieldValidationException("leaseId", "That lease is not in this property.");
        if (i.LeaseId is { } lid && i.UnitId is { } uid && !await leases.AnyAsync(l => l.Id == lid && l.UnitId == uid, ct)) throw new FieldValidationException("leaseId", "That lease is not for the chosen unit.");
        x.LegalEntityId = property.LegalEntityId; x.PropertyId = i.PropertyId; x.UnitId = i.UnitId; x.LeaseId = i.LeaseId; x.Kind = i.Kind;
        x.InspectionDate = i.InspectionDate; x.Inspector = i.Inspector; x.Notes = i.Notes;
    }

    public static List<InspectionItem> Items(Inspection i, InspectionInput input)
        => input.Items.Select(t => new InspectionItem { InspectionId = i.Id, Area = t.Area.Trim(), Condition = t.Condition, Notes = t.Notes, RepairCost = t.RepairCost }).ToList();
}
