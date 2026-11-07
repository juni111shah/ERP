using ERP.Application.Abstractions;
using ERP.Application.Dashboard;
using ERP.Application.Search;
using ERP.Modules.IndustryPacks.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.IndustryPacks.Features;

/// <summary>Dashboard cards declared by a pack, each computed from its real records. Nothing here is a stored figure.</summary>
public sealed class PackDashboard(PackModule pack, IReadRepository<IndustryRecord> records, ICurrentUser user) : IDashboardContributor
{
    public string ModuleKey => pack.Key;

    public async Task<DashboardContribution> BuildAsync(DashboardScope scope, CancellationToken ct)
    {
        var kpis = new List<KpiCard>();
        var order = 700;
        foreach (var type in pack.Types.Where(t => t.Kpis is { Count: > 0 } && user.HasPermission(PackModule.ReadPermission(pack.Key, t.Key))))
        {
            foreach (var kpi in type.Kpis!)
            {
                var statuses = kpi.Statuses?.ToArray();
                var query = records.Query().Where(r => r.ModuleKey == pack.Key && r.RecordType == type.Key && (scope.LegalEntityId == null || r.LegalEntityId == scope.LegalEntityId));
                if (statuses is not null) query = query.Where(r => statuses.Contains(r.Status));
                if (kpi.OverdueOnly) query = query.Where(r => r.DueDate != null && r.DueDate < scope.Today);
                var value = kpi.SumAmount ? (await query.Select(r => r.Amount).ToListAsync(ct)).Sum() : await query.CountAsync(ct);
                kpis.Add(new KpiCard($"{pack.Key}-{kpi.Key}", kpi.Label, value, kpi.SumAmount ? "money" : "number", kpi.Caption, order++, $"/industry/{pack.Key}/{type.Key}"));
            }
        }
        return new DashboardContribution(kpis, [], []);
    }
}

public sealed class PackSearch(PackModule pack, IReadRepository<IndustryRecord> records, ICurrentUser user) : ISearchProvider
{
    public string? ModuleKey => pack.Key;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string term, int limit, CancellationToken ct)
    {
        var readable = pack.Types.Where(t => !t.Sensitive && user.HasPermission(PackModule.ReadPermission(pack.Key, t.Key))).ToDictionary(t => t.Key);
        if (readable.Count == 0) return [];
        var keys = readable.Keys.ToArray();
        var rows = await records.Query().Where(r => r.ModuleKey == pack.Key && keys.Contains(r.RecordType) && (r.Title.Contains(term) || r.Number.Contains(term)))
            .OrderByDescending(r => r.RecordDate).Take(limit).Select(r => new { r.RecordType, r.Number, r.Title, r.Status, r.Id }).ToListAsync(ct);
        return rows.Select(r => new SearchHit(readable[r.RecordType].Label, $"{r.Number} · {r.Title}", r.Status, $"/industry/{pack.Key}/{r.RecordType}/{r.Id}")).ToList();
    }
}
