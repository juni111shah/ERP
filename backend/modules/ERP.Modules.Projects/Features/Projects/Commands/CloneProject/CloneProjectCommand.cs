using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Copies a project (or template) with its milestones, tasks, dependencies, rates and optionally its team, moved to a new start date.</summary>
public sealed record CloneProjectCommand(Guid Id, CloneInput Input) : ICommand<ProjectDto>, IRequirePermission
{
    public string RequiredPermission => P.ProjectsManage;
}
