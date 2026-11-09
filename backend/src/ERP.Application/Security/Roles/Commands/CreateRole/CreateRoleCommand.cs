using ERP.Application.Common;

namespace ERP.Application.Security;

/// <summary>Creates a custom role. The caller can only put in permissions they hold themselves.</summary>
public sealed record CreateRoleCommand(RoleInput Input) : ICommand<CreatedRoleDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.RolesWrite;
}
