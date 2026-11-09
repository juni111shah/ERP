using ERP.Application.Common;

namespace ERP.Application.Security;

/// <summary>Adds a person to the workspace (creating their account when they have none) with roles and, optionally, only some legal entities.</summary>
public sealed record InviteUserCommand(InviteUserInput Input) : ICommand<UserAccount>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.UsersWrite;
}
