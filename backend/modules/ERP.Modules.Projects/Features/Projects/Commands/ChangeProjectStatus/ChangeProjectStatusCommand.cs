using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Features;

/// <summary>Moves a project to another status; completing or cancelling it releases what is left in work in progress.</summary>
public sealed record ChangeProjectStatusCommand(Guid Id, ProjectStatus Status) : ICommand<ProjectDto>, IRequirePermission
{
    public string RequiredPermission => P.ProjectsManage;
}
