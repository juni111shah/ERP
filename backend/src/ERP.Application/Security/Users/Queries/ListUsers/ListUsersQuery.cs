using ERP.Application.Common;

namespace ERP.Application.Security;

/// <summary>The members of the current workspace with their roles.</summary>
public sealed record ListUsersQuery : IQuery<IReadOnlyList<UserSummary>>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.UsersRead;
}
