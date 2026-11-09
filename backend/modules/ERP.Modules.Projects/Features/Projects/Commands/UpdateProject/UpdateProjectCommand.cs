using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Edits a project; closing it through the form releases what is left in work in progress.</summary>
public sealed record UpdateProjectCommand(Guid Id, ProjectInput Input) : ICommand<ProjectDto>, IRequirePermission
{
    public string RequiredPermission => P.ProjectsManage;
}
