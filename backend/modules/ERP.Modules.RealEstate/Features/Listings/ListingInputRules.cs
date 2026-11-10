using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

/// <summary>Checks what a listing form refers to and copies it onto the listing (shared by create and update).</summary>
internal sealed class ListingInputRules(IReadRepository<Property> properties, IReadRepository<Unit> units)
{
    public async Task MapAsync(ListingInput i, Listing l, CancellationToken ct)
    {
        var property = await properties.FindAsync(i.PropertyId, ct) ?? throw new FieldValidationException("propertyId", "Property not found.");
        if (i.UnitId is { } unitId && !await units.AnyAsync(u => u.Id == unitId && u.PropertyId == i.PropertyId, ct)) throw new FieldValidationException("unitId", "That unit is not in this property.");
        l.LegalEntityId = property.LegalEntityId; l.PropertyId = i.PropertyId; l.UnitId = i.UnitId; l.Kind = i.Kind; l.Title = i.Title.Trim();
        l.Price = i.Price; l.Channels = i.Channels; l.Description = i.Description;
    }
}
