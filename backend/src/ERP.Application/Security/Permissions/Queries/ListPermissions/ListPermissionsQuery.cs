using ERP.Application.Common;

namespace ERP.Application.Security;

/// <summary>Every permission of the core and the installed modules.</summary>
public sealed record ListPermissionsQuery : IQuery<IReadOnlyList<PermissionDefinition>>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.RolesRead;
}
