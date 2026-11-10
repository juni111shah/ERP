using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>What happened on a project, newest first, read from the audit trail.</summary>
public sealed record GetProjectActivityQuery(Guid ProjectId, int Take = 100) : IQuery<IReadOnlyList<ActivityDto>>, IRequirePermission
{
    public string RequiredPermission => P.ProjectsRead;
}
