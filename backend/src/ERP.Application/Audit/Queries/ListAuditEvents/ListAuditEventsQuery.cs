using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Audit;

/// <summary>The audit log, newest first by default. Search, filter and sort come from the [Searchable]/[Filterable]/[Sortable] fields of <see cref="ERP.Domain.Platform.AuditEvent"/>.</summary>
public sealed record ListAuditEventsQuery : PagedQuery, IQuery<PagedResult<AuditRow>>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.AuditRead;
}
