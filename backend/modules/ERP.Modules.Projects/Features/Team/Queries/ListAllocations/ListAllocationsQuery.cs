using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>The planned hours per week of people on projects, filtered by project and/or person.</summary>
public sealed record ListAllocationsQuery(Guid? ProjectId, Guid? UserId) : IQuery<IReadOnlyList<AllocationDto>>, IRequirePermission
{
    public string RequiredPermission => P.ProjectsRead;
}
