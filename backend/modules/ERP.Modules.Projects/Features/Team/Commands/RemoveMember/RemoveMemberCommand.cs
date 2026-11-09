using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Takes a person off a project's team.</summary>
public sealed record RemoveMemberCommand(Guid Id) : ICommand, IRequirePermission
{
    public string RequiredPermission => P.TeamManage;
}
