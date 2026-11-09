using ERP.Application.Common;
using ERP.Domain.Customization;

namespace ERP.Application.Customization;

internal sealed class UpdateCustomFieldDefinitionHandler(ICustomFieldDefinitionRepository definitions) : ICommandHandler<UpdateCustomFieldDefinitionCommand, CustomFieldDefinitionDto>
{
    public async Task<CustomFieldDefinitionDto> Handle(UpdateCustomFieldDefinitionCommand command, CancellationToken ct)
    {
        // 1. Load it; key and entity type never change, and the data type only widens.
        var def = await definitions.GetForUpdateAsync(command.Id, ct);
        var i = command.Input;
        if (i.Key != def.Key) throw new FieldValidationException("key", "The key cannot be changed after the field is created.");
        if (i.EntityType != def.EntityType) throw new FieldValidationException("entityType", "The entity type cannot be changed.");
        if (i.DataType != def.DataType && !IsLosslessWidening(def.DataType, i.DataType))
            throw new ConflictException($"Changing a field from {def.DataType} to {i.DataType} could lose data.");

        // 2. Apply, check it as a whole and save.
        def.Label = i.Label.Trim(); def.DataType = i.DataType; def.Required = i.Required; def.Options = i.Options?.ToJsonString();
        def.Order = i.Order; def.Section = i.Section; def.HelpText = i.HelpText; def.IsSearchable = i.IsSearchable; def.IsActive = i.IsActive;
        def.DefaultValue = i.DefaultValue?.ToJsonString(); def.LegalEntityId = i.LegalEntityId; def.ShowInList = i.ShowInList; def.Sortable = i.Sortable && i.IsSearchable;
        CustomFieldDefinitionRules.ThrowIfInvalid(def);
        await definitions.SaveChangesAsync(ct);
        return def.ToDto();
    }

    private static bool IsLosslessWidening(CustomFieldDataType from, CustomFieldDataType to)
        => (from, to) is (CustomFieldDataType.Number, CustomFieldDataType.Decimal) or (CustomFieldDataType.Select, CustomFieldDataType.MultiSelect);
}
