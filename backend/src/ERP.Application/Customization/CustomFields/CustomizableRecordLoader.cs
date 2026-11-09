using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Common;

namespace ERP.Application.Customization;

/// <summary>Loads any customizable record for its custom-field values, after checking the caller may read (or write) that entity type.</summary>
public sealed class CustomizableRecordLoader(IEntityStore entities, ICustomizableEntityRegistry registry, ICurrentUser user)
{
    public async Task<(CustomizableEntityDefinition, IHasCustomFields)> LoadAsync(string entityType, Guid id, bool write, CancellationToken ct)
    {
        var def = registry.Find(entityType) ?? throw new NotFoundException($"'{entityType}' does not support custom fields.");
        if (def.Permission is { } perm)
        {
            var needed = write ? perm.Replace(".read", ".write") : perm;
            if (!user.HasPermission(needed)) throw new ForbiddenException();
        }
        var entity = await entities.FindForUpdateAsync(def.ClrType, id, ct) as IHasCustomFields ?? throw new NotFoundException();
        return (def, entity);
    }
}
