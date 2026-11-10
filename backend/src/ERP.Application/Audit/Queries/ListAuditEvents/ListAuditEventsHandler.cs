using ERP.Application.Common;

namespace ERP.Application.Audit;

internal sealed class ListAuditEventsHandler(IAuditEventRepository audits, AuditRows rows) : IQueryHandler<ListAuditEventsQuery, PagedResult<AuditRow>>
{
    public async Task<PagedResult<AuditRow>> Handle(ListAuditEventsQuery query, CancellationToken ct)
    {
        var page = await audits.PageAsync(query, ct: ct);
        var names = await rows.UserNamesAsync(page.Items, ct);
        return page.Map(a => rows.ToRow(a, names));
    }
}
