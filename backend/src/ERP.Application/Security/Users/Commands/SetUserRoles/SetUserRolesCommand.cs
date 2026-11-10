using ERP.Application.Common;

namespace ERP.Application.Security;

/// <summary>Replaces the roles of a workspace member.</summary>
public sealed record SetUserRolesCommand(Guid UserId, Guid[] RoleIds) : ICommand<UserSummary>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.UsersWrite;
}
