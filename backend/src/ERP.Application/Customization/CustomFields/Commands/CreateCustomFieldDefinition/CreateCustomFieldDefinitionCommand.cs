using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Customization;

/// <summary>Adds a custom field to an entity type (unique key, at most the allowed number of active fields).</summary>
public sealed record CreateCustomFieldDefinitionCommand(CustomFieldDefinitionInput Input) : ICommand<CustomFieldDefinitionDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.CustomFieldsManage;
}
