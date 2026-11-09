using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>One project with its figures and custom fields.</summary>
public sealed record GetProjectQuery(Guid Id) : IQuery<ProjectDto>, IRequirePermission
{
    public string RequiredPermission => P.ProjectsRead;
}
