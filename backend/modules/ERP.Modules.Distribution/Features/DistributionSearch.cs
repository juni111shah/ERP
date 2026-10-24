using ERP.Application.Abstractions;
using ERP.Application.Search;
using ERP.Modules.Distribution.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Distribution.Features;

public sealed class DistributionSearch(
    IReadRepository<DistributionRoute> routes,
    IReadRepository<VanSalesTrip> trips,
    IReadRepository<WholesalePriceMatrix> wholesale,
    ICurrentUser user) : ISearchProvider
{
    public string? ModuleKey => DistributionModule.Key;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string term, int limit, CancellationToken ct)
    {
        var hits = new List<SearchHit>();

        if (user.HasPermission(Permissions.RoutesRead))
        {
            var rList = await routes.Query(new Specification<DistributionRoute>(r =>
                r.Name.Contains(term) || r.Code.Contains(term)))
                .Take(limit)
                .Select(r => new { r.Id, r.Code, r.Name, r.DriverName })
                .ToListAsync(ct);

            hits.AddRange(rList.Select(r => new SearchHit("Distribution Routes", $"{r.Code} · {r.Name}", r.DriverName ?? "Unassigned", "/distribution/routes")));
        }

        if (user.HasPermission(Permissions.TripsRead) && hits.Count < limit)
        {
            var remaining = limit - hits.Count;
            var tList = await trips.Query(new Specification<VanSalesTrip>(t =>
                t.TripNumber.Contains(term) || t.DriverName.Contains(term) || t.VehicleNumber.Contains(term)))
                .Take(remaining)
                .Select(t => new { t.Id, t.TripNumber, t.DriverName, t.Status })
                .ToListAsync(ct);

            hits.AddRange(tList.Select(t => new SearchHit("Van Sales Trips", $"{t.TripNumber} · {t.DriverName}", t.Status.ToString(), "/distribution/trips")));
        }

        if (user.HasPermission(Permissions.PricingRead) && hits.Count < limit)
        {
            var remaining = limit - hits.Count;
            var wList = await wholesale.Query(new Specification<WholesalePriceMatrix>(w =>
                w.ItemName.Contains(term) || w.ItemCode.Contains(term)))
                .Take(remaining)
                .Select(w => new { w.Id, w.ItemCode, w.ItemName, w.Tier })
                .ToListAsync(ct);

            hits.AddRange(wList.Select(w => new SearchHit("Wholesale Pricing", $"{w.ItemCode} · {w.ItemName}", w.Tier.ToString(), "/distribution/wholesale")));
        }

        return hits;
    }
}
