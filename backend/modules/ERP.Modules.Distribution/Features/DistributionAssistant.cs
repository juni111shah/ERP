using ERP.Application.Abstractions;
using ERP.Application.Assistant;
using ERP.Domain.Organizations;
using ERP.Modules.Distribution.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Distribution.Features;

/// <summary>Counts a route's stops but never reads them: a stop holds the customer's address and phone.</summary>
internal sealed class RoutesAssistantReader(IReadRepository<DistributionRoute> routes) : IAssistantReader
{
    public string Key => "distribution_routes";
    public string Description => "Delivery routes: code, name, driver, vehicle, number of stops and whether active.";
    public string? ModuleKey => DistributionModule.Key;
    public string Permission => Permissions.RoutesRead;

    public async Task<object> ListAsync(string? search, int take, CancellationToken ct)
    {
        var query = routes.Query();
        if (search is not null) query = query.Where(r => r.Code.Contains(search) || r.Name.Contains(search) || (r.DriverName != null && r.DriverName.Contains(search)));
        return await query.OrderBy(r => r.Code).Take(take).Select(r => new { r.Code, r.Name, Driver = r.DriverName, Vehicle = r.VehicleNumber, Stops = r.Stops.Count, r.IsActive, Link = "/distribution/routes" }).ToListAsync(ct);
    }
}

internal sealed class VanTripsAssistantReader(IReadRepository<VanSalesTrip> trips, IReadRepository<LegalEntity> entities) : IAssistantReader
{
    public string Key => "van_trips";
    public string Description => "Van sales trips, newest first: number, route, driver, vehicle, dates, status, sales and collections.";
    public string? ModuleKey => DistributionModule.Key;
    public string Permission => Permissions.TripsRead;

    public async Task<object> ListAsync(string? search, int take, CancellationToken ct)
    {
        var query = trips.Query();
        if (search is not null) query = query.Where(t => t.TripNumber.Contains(search) || t.RouteName.Contains(search) || t.DriverName.Contains(search));
        return await query.OrderByDescending(t => t.DepartureDate).Take(take).Select(t => new { Number = t.TripNumber, Route = t.RouteName, Driver = t.DriverName, Vehicle = t.VehicleNumber, t.DepartureDate, t.ReturnDate, Status = t.Status.ToString(), Currency = entities.Query(null).Where(e => e.Id == t.LegalEntityId).Select(e => e.BaseCurrency).FirstOrDefault(), t.TotalSalesAmount, t.TotalCollectionsAmount, Link = "/distribution/trips" }).ToListAsync(ct);
    }
}

internal static class DistributionAssistantHelp
{
    public static readonly IReadOnlyList<AssistantHelpArticle> Articles =
    [
        new("Create a delivery route", "Go to Distribution > Routes, press Add Route, fill it in and press Save. Open the route and press Add Stop to add its customers.", "/distribution/routes", Permissions.RoutesRead),
        new("Dispatch a van trip", "Go to Distribution > Van sales and press Dispatch Van Trip. Choose the route and warehouse, load items with Add To Van and press Dispatch.", "/distribution/trips", Permissions.TripsRead),
        new("Reconcile a van trip", "On Distribution > Van sales press Reconcile on the trip, enter what was sold and returned, then press Complete trip.", "/distribution/trips", Permissions.TripsRead),
        new("Set wholesale prices", "Go to Distribution > Wholesale pricing, press Add Pricing Tier, fill in the item, tier and price and press Save. Calculate Wholesale Rate checks the price for a quantity.", "/distribution/wholesale", Permissions.PricingRead),
    ];
}
