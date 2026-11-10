using ERP.Application.Common;

namespace ERP.Application.Customization;

internal sealed class DeactivateCustomFieldDefinitionHandler(ICustomFieldDefinitionRepository definitions) : ICommandHandler<DeactivateCustomFieldDefinitionCommand, CustomFieldDefinitionDto>
{
    public async Task<CustomFieldDefinitionDto> Handle(DeactivateCustomFieldDefinitionCommand command, CancellationToken ct)
    {
        var def = await definitions.GetForUpdateAsync(command.Id, ct);
        def.IsActive = false;
        await definitions.SaveChangesAsync(ct);
        return def.ToDto();
    }
}
