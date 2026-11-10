using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Customization;

/// <summary>Entity types that accept custom fields (core and installed modules).</summary>
public sealed record ListCustomizableEntitiesQuery : IQuery<IReadOnlyList<CustomizableEntityDto>>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.CustomFieldsRead;
}
