using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Puts a workspace member on the team of an open project.</summary>
public sealed record AddMemberCommand(Guid ProjectId, Guid UserId, string? Role) : ICommand<MemberDto>, IRequirePermission
{
    public string RequiredPermission => P.TeamManage;
}
