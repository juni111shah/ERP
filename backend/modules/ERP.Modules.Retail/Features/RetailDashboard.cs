using ERP.Application.Abstractions;
using ERP.Application.Dashboard;
using ERP.Modules.Retail.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Retail.Features;

public sealed class RetailDashboard(
    IReadRepository<RetailStore> stores,
    IReadRepository<RetailPromotion> promotions,
    ICurrentUser user) : IDashboardContributor
{
    public string ModuleKey => RetailModule.Key;

    public async Task<DashboardContribution> BuildAsync(DashboardScope scope, CancellationToken ct)
    {
        var entity = scope.LegalEntityId;
        var kpis = new List<KpiCard>();

        if (user.HasPermission(Permissions.StoresRead))
        {
            var activeStores = await stores.CountAsync(s => s.IsActive && (entity == null || s.LegalEntityId == entity), ct);
            kpis.Add(new("retail-active-stores", "Retail Stores", activeStores, "number", "branches in operation", 150, "/retail/stores"));
        }

        if (user.HasPermission(Permissions.PromotionsRead))
        {
            var activePromos = await promotions.CountAsync(p => p.IsActive && (entity == null || p.LegalEntityId == entity), ct);
            kpis.Add(new("retail-active-promotions", "Active Promotions", activePromos, "number", "campaigns & promo codes", 155, "/retail/promotions"));
        }

        return new DashboardContribution(kpis, [], []);
    }
}
