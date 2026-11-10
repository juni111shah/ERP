using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Audit;

/// <summary>One event: every field before and after with sensitive values masked, and what else the same save wrote.</summary>
public sealed record GetAuditEventQuery(Guid Id) : IQuery<AuditEventDetail>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.AuditRead;
}
