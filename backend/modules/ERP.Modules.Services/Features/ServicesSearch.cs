using ERP.Application.Abstractions;
using ERP.Application.Search;
using ERP.Modules.Services.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Services.Features;

public sealed class ServicesSearch(
    IReadRepository<ServiceEngagement> engagements,
    IReadRepository<RetainerContract> retainers,
    ICurrentUser user) : ISearchProvider
{
    public string? ModuleKey => ServicesModule.Key;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string term, int limit, CancellationToken ct)
    {
        var hits = new List<SearchHit>();

        if (user.HasPermission(Permissions.EngagementsRead))
        {
            var engList = await engagements.Query(new Specification<ServiceEngagement>(e =>
                e.Title.Contains(term) || e.EngagementCode.Contains(term) || e.ClientName.Contains(term)))
                .Take(limit)
                .Select(e => new { e.Id, e.EngagementCode, e.Title, e.ClientName, e.Status })
                .ToListAsync(ct);

            hits.AddRange(engList.Select(e => new SearchHit("Engagements", $"{e.EngagementCode} · {e.Title}", $"{e.ClientName} ({e.Status})", "/services/engagements")));
        }

        if (user.HasPermission(Permissions.RetainersRead) && hits.Count < limit)
        {
            var remaining = limit - hits.Count;
            var retList = await retainers.Query(new Specification<RetainerContract>(r =>
                r.Title.Contains(term) || r.RetainerNumber.Contains(term) || r.ClientName.Contains(term)))
                .Take(remaining)
                .Select(r => new { r.Id, r.RetainerNumber, r.Title, r.ClientName, r.Status })
                .ToListAsync(ct);

            hits.AddRange(retList.Select(r => new SearchHit("Retainers", $"{r.RetainerNumber} · {r.Title}", $"{r.ClientName} ({r.Status})", "/services/retainers")));
        }

        return hits;
    }
}
