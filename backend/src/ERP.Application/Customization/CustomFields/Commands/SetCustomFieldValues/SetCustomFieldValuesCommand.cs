using System.Text.Json.Nodes;
using ERP.Application.Common;

namespace ERP.Application.Customization;

/// <summary>Validates and saves custom-field values on any customizable record (the caller needs the entity type's write permission).</summary>
public sealed record SetCustomFieldValuesCommand(string EntityType, Guid RecordId, JsonObject Values) : ICommand<JsonObject>;
