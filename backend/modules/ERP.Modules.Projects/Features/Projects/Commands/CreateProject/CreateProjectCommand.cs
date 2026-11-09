using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Creates a project, numbered from the project sequence unless a number is given.</summary>
public sealed record CreateProjectCommand(ProjectInput Input) : ICommand<ProjectDto>, IRequirePermission
{
    public string RequiredPermission => P.ProjectsManage;
}
