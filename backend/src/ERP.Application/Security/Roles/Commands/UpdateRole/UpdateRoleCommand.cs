using ERP.Application.Common;

namespace ERP.Application.Security;

/// <summary>Edits a custom role. Built-in roles (from the core or an industry template) are read-only: create a copy instead.</summary>
public sealed record UpdateRoleCommand(Guid Id, RoleInput Input) : ICommand<CreatedRoleDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.RolesWrite;
}
