using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Organizations;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class CreatePropertyHandler(
    IRepository<Property> properties, PropertyInputRules rules, INumberingService numbers, ICustomFieldService fields, ILegalEntityGuard legalEntityGuard)
    : ICommandHandler<CreatePropertyCommand, PropertyDto>
{
    public async Task<PropertyDto> Handle(CreatePropertyCommand cmd, CancellationToken ct)
    {
        // 1. The legal entity and the owner must be valid.
        var i = cmd.Input;
        await legalEntityGuard.RequireAsync(i.LegalEntityId, ct);
        await rules.EnsureOwnerAsync(i, ct);

        // 2. Number it, apply the form and the custom fields, save.
        var property = new Property { Code = await numbers.NextAsync(Docs.Property, i.LegalEntityId, ct: ct), LegalEntityId = i.LegalEntityId };
        PropertyInputRules.Map(i, property);
        await fields.ApplyAsync(Docs.Property, property, i.CustomFields, isCreate: true, ct);
        properties.Add(property);
        await properties.SaveChangesAsync(ct);
        return PropertyDto.From(property, await fields.ReadAsync(Docs.Property, property, ct));
    }
}
