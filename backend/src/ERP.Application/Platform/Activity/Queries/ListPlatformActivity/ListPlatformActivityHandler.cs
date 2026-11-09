using ERP.Application.Common;

namespace ERP.Application.Platform;

internal sealed class ListPlatformActivityHandler(IPlatformActivityRepository activity) : IQueryHandler<ListPlatformActivityQuery, PagedResult<ActivityRow>>
{
    public async Task<PagedResult<ActivityRow>> Handle(ListPlatformActivityQuery query, CancellationToken ct)
    {
        var (rows, total) = await activity.ListPageAsync(query.OrgId, query.Search, query.PageNumber, query.Size, ct);
        return new(rows.Select(ActivityRow.From).ToList(), total, query.PageNumber, query.Size);
    }
}
