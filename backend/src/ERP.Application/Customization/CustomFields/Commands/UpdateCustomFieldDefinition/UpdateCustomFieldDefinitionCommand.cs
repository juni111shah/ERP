using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Customization;

/// <summary>Changes a custom field; its key and entity type are fixed and its data type may only widen without losing data.</summary>
public sealed record UpdateCustomFieldDefinitionCommand(Guid Id, CustomFieldDefinitionInput Input) : ICommand<CustomFieldDefinitionDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.CustomFieldsManage;
}
