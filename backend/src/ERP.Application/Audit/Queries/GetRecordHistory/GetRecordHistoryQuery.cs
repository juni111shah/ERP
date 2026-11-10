using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Audit;

/// <summary>Who changed a record, when, and each field from old to new (from the audit trail). Newest first.</summary>
public sealed record GetRecordHistoryQuery(string RecordType, string RecordId, int Page = 1, int PageSize = 25) : IQuery<PagedResult<HistoryEntry>>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.AuditRead;
}
