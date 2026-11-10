using System.Text.Json.Nodes;
using ERP.Domain.Customization;

namespace ERP.Application.Customization;

public sealed record CustomFieldDefinitionDto(
    Guid Id, string EntityType, string Key, string Label, string DataType, bool Required, JsonNode? Options,
    int Order, string? Section, string? HelpText, bool IsSearchable, bool IsActive, string? ModuleKey,
    JsonNode? DefaultValue, Guid? LegalEntityId, bool ShowInList, bool Sortable);

public sealed record CustomFieldDefinitionInput(
    string EntityType, string Key, string Label, CustomFieldDataType DataType, bool Required, JsonNode? Options,
    int Order, string? Section, string? HelpText, bool IsSearchable, bool IsActive = true,
    JsonNode? DefaultValue = null, Guid? LegalEntityId = null, bool ShowInList = false, bool Sortable = false);

public sealed record CustomizableEntityDto(string Key, string Label, string Module);

internal static class CustomFieldMapping
{
    public static CustomFieldDefinitionDto ToDto(this CustomFieldDefinition d) => new(
        d.Id, d.EntityType, d.Key, d.Label, System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(d.DataType.ToString()), d.Required,
        string.IsNullOrWhiteSpace(d.Options) ? null : JsonNode.Parse(d.Options),
        d.Order, d.Section, d.HelpText, d.IsSearchable, d.IsActive, d.ModuleKey,
        string.IsNullOrWhiteSpace(d.DefaultValue) ? null : JsonNode.Parse(d.DefaultValue), d.LegalEntityId, d.ShowInList, d.Sortable);
}
