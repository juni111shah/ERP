using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>The headline figures of the projects list: counts, budget, hours, unbilled work and health.</summary>
public sealed record GetProjectKpiSummaryQuery() : IQuery<ProjectKpiDto>, IRequirePermission
{
    public string RequiredPermission => P.ProjectsRead;
}
