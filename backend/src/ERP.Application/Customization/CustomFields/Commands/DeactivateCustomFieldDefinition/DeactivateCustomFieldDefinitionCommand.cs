using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Customization;

/// <summary>Archives a field: hidden from forms, values kept. Hard delete is intentionally not offered in sprint 0.</summary>
public sealed record DeactivateCustomFieldDefinitionCommand(Guid Id) : ICommand<CustomFieldDefinitionDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.CustomFieldsManage;
}
