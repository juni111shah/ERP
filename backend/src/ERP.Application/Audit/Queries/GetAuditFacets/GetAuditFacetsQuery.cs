using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Audit;

/// <summary>What the log can be filtered by in this workspace, with a short summary of recent activity.</summary>
public sealed record GetAuditFacetsQuery : IQuery<AuditFacets>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.AuditRead;
}
