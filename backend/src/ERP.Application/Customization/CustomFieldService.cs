using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Common;
using ERP.Domain.Customization;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Customization;

/// <summary>Loads definitions and validates/merges values for one record. Shared by every feature that writes IHasCustomFields entities.</summary>
public interface ICustomFieldService
{
    Task<IReadOnlyList<CustomFieldDefinition>> GetDefinitionsAsync(string entityType, CancellationToken ct = default);
    /// <summary>Validates incoming values and writes the merged JSON to the entity, or throws FieldValidationException.</summary>
    Task ApplyAsync(string entityType, IHasCustomFields entity, JsonObject? values, bool isCreate, CancellationToken ct = default);
    Task<JsonObject> ReadAsync(string entityType, IHasCustomFields entity, CancellationToken ct = default);
}

public sealed class CustomFieldService(IReadRepository<CustomFieldDefinition> definitions, IEntityStore entities, ICustomizableEntityRegistry registry) : ICustomFieldService
{
    public async Task<IReadOnlyList<CustomFieldDefinition>> GetDefinitionsAsync(string entityType, CancellationToken ct = default)
        => await definitions.Query(new Specification<CustomFieldDefinition>(d => d.EntityType == entityType)).OrderBy(d => d.Order).ThenBy(d => d.Label).ToListAsync(ct);

    public async Task ApplyAsync(string entityType, IHasCustomFields entity, JsonObject? values, bool isCreate, CancellationToken ct = default)
    {
        var defs = CustomFieldValidator.ForLegalEntity(await GetDefinitionsAsync(entityType, ct), (entity as ERP.Domain.Common.ILegalEntityScoped)?.LegalEntityId);
        // Resolve lookup targets up-front (async) so the validator stays synchronous.
        var lookupIds = new Dictionary<(string, Guid), bool>();
        if (values is not null)
        {
            foreach (var def in defs.Where(d => d.DataType == CustomFieldDataType.Lookup && values.ContainsKey(d.Key)))
            {
                var target = CustomFieldOptions.Parse(def.Options).Target ?? "";
                if (values[def.Key] is JsonValue v && v.TryGetValue<string>(out var s) && Guid.TryParse(s, out var id))
                    lookupIds[(target, id)] = await ExistsAsync(target, id, ct);
            }
        }
        var result = CustomFieldValidator.Validate(defs, values, entity.CustomFields, isCreate,
            (target, id) => lookupIds.TryGetValue((target, id), out var ok) && ok);
        if (!result.IsValid) throw new FieldValidationException(result.Errors.ToDictionary(k => k.Key, v => v.Value));
        entity.CustomFields = result.Json;
    }

    public async Task<JsonObject> ReadAsync(string entityType, IHasCustomFields entity, CancellationToken ct = default)
        => CustomFieldValidator.Project(entity.CustomFields, await GetDefinitionsAsync(entityType, ct));

    private async Task<bool> ExistsAsync(string entityType, Guid id, CancellationToken ct)
    {
        var def = registry.Find(entityType);
        if (def is null) return false;
        // Global query filters (workspace, legal entity, soft delete) apply, so ids from another workspace are "not found".
        return await entities.ExistsAsync(def.ClrType, id, ct);
    }
}
