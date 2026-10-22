using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Common;
using ERP.Domain.Customization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Customization;

public sealed record CustomFieldDefinitionDto(
    Guid Id, string EntityType, string Key, string Label, string DataType, bool Required, JsonNode? Options,
    int Order, string? Section, string? HelpText, bool IsSearchable, bool IsActive, string? ModuleKey,
    JsonNode? DefaultValue, Guid? LegalEntityId, bool ShowInList, bool Sortable);

public sealed record CustomFieldDefinitionInput(
    string EntityType, string Key, string Label, CustomFieldDataType DataType, bool Required, JsonNode? Options,
    int Order, string? Section, string? HelpText, bool IsSearchable, bool IsActive = true,
    JsonNode? DefaultValue = null, Guid? LegalEntityId = null, bool ShowInList = false, bool Sortable = false);

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

internal static class CustomFieldMapping
{
    public static CustomFieldDefinitionDto ToDto(this CustomFieldDefinition d) => new(
        d.Id, d.EntityType, d.Key, d.Label, System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(d.DataType.ToString()), d.Required,
        string.IsNullOrWhiteSpace(d.Options) ? null : JsonNode.Parse(d.Options),
        d.Order, d.Section, d.HelpText, d.IsSearchable, d.IsActive, d.ModuleKey,
        string.IsNullOrWhiteSpace(d.DefaultValue) ? null : JsonNode.Parse(d.DefaultValue), d.LegalEntityId, d.ShowInList, d.Sortable);
}

// ---- Queries ---------------------------------------------------------------

public sealed record ListCustomFieldDefinitions(string EntityType, bool IncludeInactive = false) : IRequest<IReadOnlyList<CustomFieldDefinitionDto>>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.CustomFieldsRead;
}

/// <summary>Active definitions for rendering forms; any signed-in user who can see the entity can read them.</summary>
public sealed record GetFormFields(string EntityType) : IQuery<IReadOnlyList<CustomFieldDefinitionDto>>;

public sealed record CustomizableEntityDto(string Key, string Label, string Module);

/// <summary>Entity types that accept custom fields (core and installed modules).</summary>
public sealed record ListCustomizableEntities : IQuery<IReadOnlyList<CustomizableEntityDto>>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.CustomFieldsRead;
}

internal sealed class CustomizableEntityHandler(ICustomizableEntityRegistry registry) : IRequestHandler<ListCustomizableEntities, IReadOnlyList<CustomizableEntityDto>>
{
    public Task<IReadOnlyList<CustomizableEntityDto>> Handle(ListCustomizableEntities q, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<CustomizableEntityDto>>(registry.All.Select(d => new CustomizableEntityDto(d.Key, d.Label, d.ModuleKey)).ToList());
}

internal sealed class CustomFieldQueryHandlers(IReadRepository<CustomFieldDefinition> definitions, ICustomizableEntityRegistry registry, ICurrentUser user) :
    IRequestHandler<ListCustomFieldDefinitions, IReadOnlyList<CustomFieldDefinitionDto>>,
    IRequestHandler<GetFormFields, IReadOnlyList<CustomFieldDefinitionDto>>
{
    public async Task<IReadOnlyList<CustomFieldDefinitionDto>> Handle(ListCustomFieldDefinitions q, CancellationToken ct)
        => (await definitions.Query(new Specification<CustomFieldDefinition>(d => d.EntityType == q.EntityType && (q.IncludeInactive || d.IsActive)))
                .OrderBy(d => d.Order).ThenBy(d => d.Label).ToListAsync(ct))
            .Select(d => d.ToDto()).ToList();

    public async Task<IReadOnlyList<CustomFieldDefinitionDto>> Handle(GetFormFields q, CancellationToken ct)
    {
        var entity = registry.Find(q.EntityType) ?? throw new NotFoundException();
        if (entity.Permission is { } permission && !user.HasPermission(permission)) throw new ForbiddenException();
        return (await definitions.Query(new Specification<CustomFieldDefinition>(d => d.EntityType == q.EntityType && d.IsActive))
                .OrderBy(d => d.Order).ThenBy(d => d.Label).ToListAsync(ct))
            .Select(d => d.ToDto()).ToList();
    }
}

// ---- Commands --------------------------------------------------------------

public sealed record CreateCustomFieldDefinition(CustomFieldDefinitionInput Input) : IRequest<CustomFieldDefinitionDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.CustomFieldsManage;
}

public sealed record UpdateCustomFieldDefinition(Guid Id, CustomFieldDefinitionInput Input) : IRequest<CustomFieldDefinitionDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.CustomFieldsManage;
}

/// <summary>Archives a field: hidden from forms, values kept. Hard delete is intentionally not offered in sprint 0.</summary>
public sealed record DeactivateCustomFieldDefinition(Guid Id) : IRequest<CustomFieldDefinitionDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.CustomFieldsManage;
}

public sealed class CustomFieldDefinitionInputValidator : AbstractValidator<CreateCustomFieldDefinition>
{
    public CustomFieldDefinitionInputValidator()
    {
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.EntityType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Input.Label).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Input.Key).NotEmpty().Matches(CustomFieldValidator.KeyFormat)
            .WithMessage("Key must be lower snake case starting with 'cf_' (max 63 characters), e.g. cf_vip_tier.");
        RuleFor(x => x.Input.DataType).IsInEnum();
    }
}

internal sealed class CustomFieldCommandHandlers(IRepository<CustomFieldDefinition> definitions, ICustomizableEntityRegistry registry) :
    IRequestHandler<CreateCustomFieldDefinition, CustomFieldDefinitionDto>,
    IRequestHandler<UpdateCustomFieldDefinition, CustomFieldDefinitionDto>,
    IRequestHandler<DeactivateCustomFieldDefinition, CustomFieldDefinitionDto>
{
    public async Task<CustomFieldDefinitionDto> Handle(CreateCustomFieldDefinition cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        if (registry.Find(i.EntityType) is null) throw new FieldValidationException("entityType", $"'{i.EntityType}' does not support custom fields.");
        if (await definitions.AnyAsync(d => d.EntityType == i.EntityType && d.Key == i.Key, ct))
            throw new ConflictException($"A field with key '{i.Key}' already exists on {i.EntityType}.");
        if (i.IsActive && await definitions.CountAsync(d => d.EntityType == i.EntityType && d.IsActive, ct) >= CustomFieldValidator.MaxFieldsPerEntityType)
            throw new ConflictException($"An entity type can have at most {CustomFieldValidator.MaxFieldsPerEntityType} active custom fields.");

        var def = new CustomFieldDefinition
        {
            EntityType = i.EntityType, Key = i.Key, Label = i.Label.Trim(), DataType = i.DataType, Required = i.Required,
            Options = i.Options?.ToJsonString(), Order = i.Order, Section = i.Section, HelpText = i.HelpText,
            IsSearchable = i.IsSearchable, IsActive = i.IsActive, DefaultValue = i.DefaultValue?.ToJsonString(),
            LegalEntityId = i.LegalEntityId, ShowInList = i.ShowInList, Sortable = i.Sortable && i.IsSearchable,
        };
        ThrowIfInvalid(def);
        definitions.Add(def);
        await definitions.SaveChangesAsync(ct);
        return def.ToDto();
    }

    public async Task<CustomFieldDefinitionDto> Handle(UpdateCustomFieldDefinition cmd, CancellationToken ct)
    {
        var def = await definitions.GetForUpdateAsync(cmd.Id, ct);
        var i = cmd.Input;
        if (i.Key != def.Key) throw new FieldValidationException("key", "The key cannot be changed after the field is created.");
        if (i.EntityType != def.EntityType) throw new FieldValidationException("entityType", "The entity type cannot be changed.");
        if (i.DataType != def.DataType && !IsLosslessWidening(def.DataType, i.DataType))
            throw new ConflictException($"Changing a field from {def.DataType} to {i.DataType} could lose data.");

        def.Label = i.Label.Trim(); def.DataType = i.DataType; def.Required = i.Required; def.Options = i.Options?.ToJsonString();
        def.Order = i.Order; def.Section = i.Section; def.HelpText = i.HelpText; def.IsSearchable = i.IsSearchable; def.IsActive = i.IsActive;
        def.DefaultValue = i.DefaultValue?.ToJsonString(); def.LegalEntityId = i.LegalEntityId; def.ShowInList = i.ShowInList; def.Sortable = i.Sortable && i.IsSearchable;
        ThrowIfInvalid(def);
        await definitions.SaveChangesAsync(ct);
        return def.ToDto();
    }

    public async Task<CustomFieldDefinitionDto> Handle(DeactivateCustomFieldDefinition cmd, CancellationToken ct)
    {
        var def = await definitions.GetForUpdateAsync(cmd.Id, ct);
        def.IsActive = false;
        await definitions.SaveChangesAsync(ct);
        return def.ToDto();
    }

    private static bool IsLosslessWidening(CustomFieldDataType from, CustomFieldDataType to)
        => (from, to) is (CustomFieldDataType.Number, CustomFieldDataType.Decimal) or (CustomFieldDataType.Select, CustomFieldDataType.MultiSelect);

    private static void ThrowIfInvalid(CustomFieldDefinition def)
    {
        var errors = CustomFieldValidator.ValidateDefinition(def);
        if (errors.Count > 0) throw new FieldValidationException(errors.ToDictionary(k => k.Key, v => v.Value));
    }
}

// ---- Values on any customizable record -------------------------------------

public sealed record GetCustomFieldValues(string EntityType, Guid RecordId) : IRequest<JsonObject>;
public sealed record SetCustomFieldValues(string EntityType, Guid RecordId, JsonObject Values) : IRequest<JsonObject>;

internal sealed class CustomFieldValueHandlers(IEntityStore entities, ICustomizableEntityRegistry registry, ICustomFieldService fields, ICurrentUser user) :
    IRequestHandler<GetCustomFieldValues, JsonObject>,
    IRequestHandler<SetCustomFieldValues, JsonObject>
{
    public async Task<JsonObject> Handle(GetCustomFieldValues q, CancellationToken ct)
    {
        var (def, entity) = await LoadAsync(q.EntityType, q.RecordId, write: false, ct);
        return await fields.ReadAsync(def.Key, entity, ct);
    }

    public async Task<JsonObject> Handle(SetCustomFieldValues cmd, CancellationToken ct)
    {
        var (def, entity) = await LoadAsync(cmd.EntityType, cmd.RecordId, write: true, ct);
        await fields.ApplyAsync(def.Key, entity, cmd.Values, isCreate: false, ct);
        await entities.SaveChangesAsync(ct);
        return await fields.ReadAsync(def.Key, entity, ct);
    }

    private async Task<(CustomizableEntityDefinition, IHasCustomFields)> LoadAsync(string entityType, Guid id, bool write, CancellationToken ct)
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
