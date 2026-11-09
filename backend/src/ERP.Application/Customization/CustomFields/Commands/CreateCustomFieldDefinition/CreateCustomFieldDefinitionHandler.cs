using ERP.Application.Common;
using ERP.Domain.Customization;

namespace ERP.Application.Customization;

internal sealed class CreateCustomFieldDefinitionHandler(ICustomFieldDefinitionRepository definitions, ICustomizableEntityRegistry registry)
    : ICommandHandler<CreateCustomFieldDefinitionCommand, CustomFieldDefinitionDto>
{
    public async Task<CustomFieldDefinitionDto> Handle(CreateCustomFieldDefinitionCommand command, CancellationToken ct)
    {
        // 1. The entity type accepts custom fields, the key is free and the active-field limit is not reached.
        var i = command.Input;
        if (registry.Find(i.EntityType) is null) throw new FieldValidationException("entityType", $"'{i.EntityType}' does not support custom fields.");
        if (await definitions.AnyAsync(d => d.EntityType == i.EntityType && d.Key == i.Key, ct))
            throw new ConflictException($"A field with key '{i.Key}' already exists on {i.EntityType}.");
        if (i.IsActive && await definitions.CountAsync(d => d.EntityType == i.EntityType && d.IsActive, ct) >= CustomFieldValidator.MaxFieldsPerEntityType)
            throw new ConflictException($"An entity type can have at most {CustomFieldValidator.MaxFieldsPerEntityType} active custom fields.");

        // 2. Build it and check it as a whole (options, default value...).
        var def = new CustomFieldDefinition
        {
            EntityType = i.EntityType, Key = i.Key, Label = i.Label.Trim(), DataType = i.DataType, Required = i.Required,
            Options = i.Options?.ToJsonString(), Order = i.Order, Section = i.Section, HelpText = i.HelpText,
            IsSearchable = i.IsSearchable, IsActive = i.IsActive, DefaultValue = i.DefaultValue?.ToJsonString(),
            LegalEntityId = i.LegalEntityId, ShowInList = i.ShowInList, Sortable = i.Sortable && i.IsSearchable,
        };
        CustomFieldDefinitionRules.ThrowIfInvalid(def);

        // 3. Save.
        definitions.Add(def);
        await definitions.SaveChangesAsync(ct);
        return def.ToDto();
    }
}
