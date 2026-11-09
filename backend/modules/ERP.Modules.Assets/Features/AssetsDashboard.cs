using ERP.Application.Abstractions;
using ERP.Application.Dashboard;
using ERP.Modules.Assets.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Assets.Features;

/// <summary>Net book value of the assets in scope at the end of the period, from the register and the depreciation actually posted, not a stored total.</summary>
public sealed class AssetsDashboard(IReadRepository<FixedAsset> assets, IReadRepository<DepreciationEntry> entries, ICurrentUser user) : IDashboardContributor
{
    public string ModuleKey => AssetsModule.Key;

    public async Task<DashboardContribution> BuildAsync(DashboardScope scope, CancellationToken ct)
    {
        var kpis = new List<KpiCard>();
        if (user.HasPermission(Permissions.RegisterRead))
        {
            var (entity, asOf) = (scope.LegalEntityId, scope.To);
            var onBooks = await assets.Query(new Specification<FixedAsset>(a => a.CapitalisedOn != null && a.CapitalisedOn <= asOf && (a.DisposedOn == null || a.DisposedOn > asOf) && (entity == null || a.LegalEntityId == entity)))
                .Select(a => new { a.Id, a.Cost }).ToListAsync(ct);
            var ids = onBooks.Select(a => a.Id).ToList();
            var charged = await entries.Query(new Specification<DepreciationEntry>(e => ids.Contains(e.AssetId) && e.PeriodEnd <= asOf)).Select(e => e.Amount).ToListAsync(ct);
            kpis.Add(new("assets-net-book-value", "Fixed assets (net book value)", onBooks.Sum(a => a.Cost) - charged.Sum(), "money", $"{onBooks.Count} assets on the books", 160, "/assets/register"));
        }
        return new DashboardContribution(kpis, [], []);
    }
}
