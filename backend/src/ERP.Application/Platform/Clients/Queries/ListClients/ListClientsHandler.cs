using ERP.Application.Common;

namespace ERP.Application.Platform;

internal sealed class ListClientsHandler(ClientReader clients) : IQueryHandler<ListClientsQuery, PagedResult<ClientRow>>
{
    public async Task<PagedResult<ClientRow>> Handle(ListClientsQuery query, CancellationToken ct)
    {
        IEnumerable<ClientRow> rows = await clients.RowsAsync(null, ct);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            rows = rows.Where(r => r.Name.Contains(s, StringComparison.OrdinalIgnoreCase) || r.Code.Contains(s, StringComparison.OrdinalIgnoreCase));
        }
        if (query.Status is { Length: > 0 } status)
        {
            if (status.Equals("none", StringComparison.OrdinalIgnoreCase)) rows = rows.Where(r => r.Status is null);
            else if (status.Equals("planRequest", StringComparison.OrdinalIgnoreCase)) rows = rows.Where(r => r.HasPlanRequest);
            else rows = rows.Where(r => string.Equals(r.Status?.ToString(), status, StringComparison.OrdinalIgnoreCase));
        }
        if (query.PlanId is { } planId) rows = rows.Where(r => r.PlanId == planId);
        rows = rows.OrderByDescending(r => r.CreatedAt);
        var list = rows.ToList();
        return new(list.Skip((query.PageNumber - 1) * query.Size).Take(query.Size).ToList(), list.Count, query.PageNumber, query.Size);
    }
}
