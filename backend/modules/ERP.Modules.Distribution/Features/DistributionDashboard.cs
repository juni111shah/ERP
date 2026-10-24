using ERP.Application.Abstractions;
using ERP.Application.Dashboard;
using ERP.Modules.Distribution.Domain;

namespace ERP.Modules.Distribution.Features;

public sealed class DistributionDashboard(
    IReadRepository<DistributionRoute> routes,
    IReadRepository<VanSalesTrip> trips,
    ICurrentUser user) : IDashboardContributor
{
    public string ModuleKey => DistributionModule.Key;

    public async Task<DashboardContribution> BuildAsync(DashboardScope scope, CancellationToken ct)
    {
        var entity = scope.LegalEntityId;
        var kpis = new List<KpiCard>();

        if (user.HasPermission(Permissions.RoutesRead))
        {
            var activeRoutes = await routes.CountAsync(r => r.IsActive && (entity == null || r.LegalEntityId == entity), ct);
            kpis.Add(new("dist-active-routes", "Delivery Routes", activeRoutes, "number", "active delivery territories", 160, "/distribution/routes"));
        }

        if (user.HasPermission(Permissions.TripsRead))
        {
            var activeTrips = await trips.CountAsync(t => t.Status == TripStatus.InProgress && (entity == null || t.LegalEntityId == entity), ct);
            kpis.Add(new("dist-active-trips", "Van Trips En Route", activeTrips, "number", "vans actively selling/delivering", 165, "/distribution/trips"));
        }

        return new DashboardContribution(kpis, [], []);
    }
}
