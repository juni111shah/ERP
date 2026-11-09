using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Customization;

/// <summary>The custom field definitions of one entity type, archived ones only when asked.</summary>
public sealed record ListCustomFieldDefinitionsQuery(string EntityType, bool IncludeInactive = false) : IQuery<IReadOnlyList<CustomFieldDefinitionDto>>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.CustomFieldsRead;
}
