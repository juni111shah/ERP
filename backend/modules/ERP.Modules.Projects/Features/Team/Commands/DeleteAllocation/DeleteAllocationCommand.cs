using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Deletes a resource allocation.</summary>
public sealed record DeleteAllocationCommand(Guid Id) : ICommand, IRequirePermission
{
    public string RequiredPermission => P.TeamManage;
}
