using ERP.Application.Security;
using ERP.Application.Common;

namespace ERP.Application.Customization;

/// <summary>Active definitions for rendering forms; any signed-in user who can see the entity can read them.</summary>
public sealed record GetFormFieldsQuery(string EntityType) : IQuery<IReadOnlyList<CustomFieldDefinitionDto>>, IAuthorizesInHandler;
