using ERP.Application.Common;

namespace ERP.Application.Security;

/// <summary>Activates or deactivates a workspace member (the account itself is kept).</summary>
public sealed record SetUserActiveCommand(Guid UserId, bool IsActive) : ICommand<UserSummary>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.UsersWrite;
}
