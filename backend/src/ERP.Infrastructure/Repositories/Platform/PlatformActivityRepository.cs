using ERP.Application.Abstractions;
using ERP.Application.Platform;
using ERP.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Platform;

internal sealed class PlatformActivityRepository(IRepository<PlatformActivity> activity) : RepositoryBase<PlatformActivity>(activity), IPlatformActivityRepository
{
    public Task<List<PlatformActivity>> ListLatestAsync(int take, CancellationToken ct) => Query().OrderByDescending(a => a.At).Take(take).ToListAsync(ct);

    public Task<List<PlatformActivity>> ListLatestForClientAsync(Guid orgId, int take, CancellationToken ct)
        => Query().Where(a => a.OrgId == orgId).OrderByDescending(a => a.At).Take(take).ToListAsync(ct);

    public async Task<(List<PlatformActivity> Rows, int Total)> ListPageAsync(Guid? orgId, string? search, int page, int size, CancellationToken ct)
    {
        var query = Query();
        if (orgId is { } id) query = query.Where(a => a.OrgId == id);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(a => a.Action.Contains(s) || a.Actor.Contains(s) || (a.ClientName != null && a.ClientName.Contains(s)) || (a.Detail != null && a.Detail.Contains(s)));
        }
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(a => a.At).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return (rows, total);
    }
}
