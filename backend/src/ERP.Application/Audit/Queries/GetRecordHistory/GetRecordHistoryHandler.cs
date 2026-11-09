using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Application.Audit;

internal sealed class GetRecordHistoryHandler(IAuditEventRepository audits, IUserDirectory directory, AuditFieldAccess access) : IQueryHandler<GetRecordHistoryQuery, PagedResult<HistoryEntry>>
{
    public async Task<PagedResult<HistoryEntry>> Handle(GetRecordHistoryQuery query, CancellationToken ct)
    {
        // 1. One page of the record's events, with the names of who wrote them.
        var total = await audits.CountAsync(a => a.RecordType == query.RecordType && a.RecordId == query.RecordId, ct);
        var page = await audits.ListPageForRecordAsync(query.RecordType, query.RecordId, query.Page, query.PageSize, ct);
        var names = (await directory.ListMembersAsync(page.Where(a => a.UserId != null).Select(a => a.UserId!.Value).Distinct().ToList(), ct)).ToDictionary(u => u.Id, u => u.FullName);

        // 2. The changes, values hidden from callers who may not read them.
        var canRead = access.CanReadValues(query.RecordType);
        var items = page.Select(a => new HistoryEntry(a.Id, a.OccurredAt, a.UserId, a.UserId is { } u ? names.GetValueOrDefault(u) : null, a.Action,
            canRead ? AuditSnapshot.Changes(a) : AuditSnapshot.Changes(a).Select(c => new FieldChange(c.Field, null, null)).ToList(), !canRead)).ToList();
        return new PagedResult<HistoryEntry>(items, total, query.Page, query.PageSize);
    }
}
