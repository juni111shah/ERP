using System.Text.Json.Nodes;
using ERP.Application.Common;

namespace ERP.Application.Customization;

/// <summary>The custom-field values of any customizable record (the caller needs the entity type's read permission).</summary>
public sealed record GetCustomFieldValuesQuery(string EntityType, Guid RecordId) : IQuery<JsonObject>;
