using ERP.Application.Common;

namespace ERP.Application.Audit;

internal sealed class GetAuditEventHandler(IAuditEventRepository audits, AuditRows rows, AuditFieldAccess access) : IQueryHandler<GetAuditEventQuery, AuditEventDetail>
{
    private const int SameSaveLimit = 25;

    public async Task<AuditEventDetail> Handle(GetAuditEventQuery query, CancellationToken ct)
    {
        // 1. The event, what else the same save wrote and how often the record changed.
        var a = await audits.GetAsync(query.Id, ct);
        var sameSave = await audits.ListSameSaveAsync(a, SameSaveLimit, ct);
        var names = await rows.UserNamesAsync([a, .. sameSave], ct);
        var count = await audits.CountAsync(x => x.RecordType == a.RecordType && x.RecordId == a.RecordId, ct);
        var related = sameSave.Select(x => rows.ToRow(x, names)).ToList();

        // 2. Values only for callers who may read them.
        return access.CanReadValues(a.RecordType)
            ? new AuditEventDetail(rows.ToRow(a, names), AuditSnapshot.Fields(a), AuditSnapshot.Masked(a.Before), AuditSnapshot.Masked(a.After), related, count)
            : new AuditEventDetail(rows.ToRow(a, names), AuditSnapshot.Fields(a).Where(f => f.Changed).Select(f => f with { Old = null, New = null }).ToList(), null, null, related, count);
    }
}
