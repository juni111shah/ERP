using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Archives a completed or cancelled project, or restores an archived one.</summary>
public sealed record ArchiveProjectCommand(Guid Id, bool Archive) : ICommand<ProjectDto>, IRequirePermission
{
    public string RequiredPermission => P.ProjectsManage;
}
