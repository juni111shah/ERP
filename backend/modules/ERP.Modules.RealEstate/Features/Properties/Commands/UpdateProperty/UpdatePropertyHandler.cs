using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Organizations;
using ERP.Modules.RealEstate.Domain;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

internal sealed class UpdatePropertyHandler(
    IRepository<Property> properties, IReadRepository<Unit> units, IReadRepository<Lease> leases, PropertyInputRules rules, ICustomFieldService fields, ILegalEntityGuard legalEntityGuard)
    : ICommandHandler<UpdatePropertyCommand, PropertyDto>
{
    public async Task<PropertyDto> Handle(UpdatePropertyCommand cmd, CancellationToken ct)
    {
        // 1. Load it with its registered units.
        var i = cmd.Input;
        var property = await properties.GetForUpdateAsync(cmd.Id, ct);
        var registered = await units.CountAsync(u => u.PropertyId == property.Id, ct);

        // 2. A change of legal entity is allowed only while nothing refers to the property.
        if (i.LegalEntityId != property.LegalEntityId)
        {
            // RE-PROP-01: the owning entity cannot change once units or leases exist.
            if (registered > 0 || await leases.AnyAsync(l => l.PropertyId == property.Id, ct))
                throw new ConflictException("The owning legal entity cannot change once the property has units or leases.");
            await legalEntityGuard.RequireAsync(i.LegalEntityId, ct);
            property.LegalEntityId = i.LegalEntityId;
        }

        // 3. The unit count cannot drop below the registered units; the owner must exist.
        if (i.TotalUnits < registered) throw new FieldValidationException("totalUnits", $"Total units cannot be below the {registered} registered units.");
        await rules.EnsureOwnerAsync(i, ct);

        // 4. Apply the form and the custom fields, save.
        PropertyInputRules.Map(i, property);
        await fields.ApplyAsync(Docs.Property, property, i.CustomFields, isCreate: false, ct);
        await properties.SaveChangesAsync(ct);
        return PropertyDto.From(property, await fields.ReadAsync(Docs.Property, property, ct));
    }
}
