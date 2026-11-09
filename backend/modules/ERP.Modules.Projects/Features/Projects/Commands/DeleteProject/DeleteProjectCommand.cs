using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Deletes a project that has nothing invoiced and no cost in the ledger.</summary>
public sealed record DeleteProjectCommand(Guid Id) : ICommand, IRequirePermission
{
    public string RequiredPermission => P.ProjectsManage;
}
