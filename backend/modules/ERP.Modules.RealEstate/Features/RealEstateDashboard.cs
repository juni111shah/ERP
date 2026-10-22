using ERP.Application.Abstractions;
using ERP.Application.Dashboard;
using ERP.Modules.RealEstate.Domain;
using Microsoft.EntityFrameworkCore;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

/// <summary>The real estate cards on the overview: portfolio value, occupancy, contracted rent, expiries and open work orders.</summary>
public sealed class RealEstateDashboard(
    IReadRepository<Property> properties, IReadRepository<Unit> units, IReadRepository<Lease> leases, IReadRepository<WorkOrder> workOrders, ISettingsService settings, ICurrentUser user) : IDashboardContributor
{
    public string ModuleKey => RealEstateModule.Key;

    public async Task<DashboardContribution> BuildAsync(DashboardScope scope, CancellationToken ct)
    {
        var entity = scope.LegalEntityId;
        var kpis = new List<KpiCard>(); var charts = new List<DashboardChart>(); var alerts = new List<DashboardAlert>();

        if (user.HasPermission(P.PropertiesRead))
        {
            var props = await properties.Query(new Specification<Property>(p => p.IsActive && (entity == null || p.LegalEntityId == entity))).Select(p => new { p.Id, p.Name, p.IndicativeValue }).ToListAsync(ct);
            kpis.Add(new("portfolio", "Portfolio value", props.Sum(p => p.IndicativeValue), "money", props.Count == 1 ? "1 property" : $"{props.Count} properties", 10, "/real-estate/properties"));
            var unitRows = await units.Query(new Specification<Unit>(u => entity == null || u.LegalEntityId == entity)).Select(u => new { u.PropertyId, u.Status }).ToListAsync(ct);
            var occupied = unitRows.Count(u => u.Status == UnitStatus.Occupied);
            kpis.Add(new("occupancy", "Occupancy", unitRows.Count == 0 ? 0 : Math.Round(100m * occupied / unitRows.Count, 1), "percent", $"{occupied} of {unitRows.Count} units let", 20, "/real-estate/units"));
            var statuses = new[] { UnitStatus.Occupied, UnitStatus.Vacant, UnitStatus.Reserved, UnitStatus.Unavailable };
            charts.Add(new("occupancy", "Occupancy by property", "stacked", props.Select(p => p.Name).ToList(),
                statuses.Select(s => new ChartSeries(s.ToString(), props.Select(p => (decimal)unitRows.Count(u => u.PropertyId == p.Id && u.Status == s)).ToList())).ToList(), "Units by status", 30));
        }

        if (user.HasPermission(P.LeasesRead))
        {
            var active = await leases.Query(new Specification<Lease>(l => (l.Status == LeaseStatus.Active || l.Status == LeaseStatus.HoldingOver) && (entity == null || l.LegalEntityId == entity)))
                .Select(l => new { l.Id, l.Number, l.UnitId, l.EndDate, l.AnnualRent, l.Status }).ToListAsync(ct);
            kpis.Add(new("contracted-rent", "Contracted annual rent", active.Sum(l => l.AnnualRent), "money", $"{active.Count} active leases", 120, "/real-estate/leases"));
            var windowDays = await settings.GetAsync<int?>("realestate.leaseExpiryAlertDays", null, ct) ?? 60;
            var expiring = active.Where(l => l.Status == LeaseStatus.Active && l.EndDate >= scope.Today && l.EndDate <= scope.Today.AddDays(windowDays)).ToList();
            kpis.Add(new("expiring", "Leases expiring", expiring.Count, "number", $"within {windowDays} days", 90, "/real-estate/leases"));
            foreach (var l in active.Where(l => l.EndDate < scope.Today).OrderBy(l => l.EndDate).Take(5))
                alerts.Add(new("critical", "Leasing", "action", $"Lease {l.Number} ended on {l.EndDate:dd MMM yyyy}. Renew or close it.", $"/real-estate/leases/{l.Id}", 10));
            foreach (var l in expiring.OrderBy(l => l.EndDate).Take(5))
                alerts.Add(new("warning", "Leasing", "upcoming", $"Lease {l.Number} expires in {l.EndDate.DayNumber - scope.Today.DayNumber} days.", $"/real-estate/leases/{l.Id}", 30));

            var months = Enumerable.Range(0, 12).Select(i => new DateOnly(scope.Today.Year, scope.Today.Month, 1).AddMonths(i)).ToList();
            charts.Add(new("lease-expiries", "Lease expiries", "bar", months.Select(m => m.ToString("MMM yy")).ToList(),
                [new("Leases ending", months.Select(m => (decimal)active.Count(l => l.EndDate.Year == m.Year && l.EndDate.Month == m.Month)).ToList())], "Active leases by month of expiry, next 12 months", 50));
        }

        if (user.HasPermission(P.WorkOrdersRead))
        {
            var open = await workOrders.Query(new Specification<WorkOrder>(w => w.Status != WorkOrderStatus.Completed && w.Status != WorkOrderStatus.Closed && w.Status != WorkOrderStatus.Cancelled && (entity == null || w.LegalEntityId == entity)))
                .Select(w => new { w.Id, w.Issue, w.Priority }).ToListAsync(ct);
            var critical = open.Where(w => w.Priority == Priority.Critical).ToList();
            kpis.Add(new("work-orders", "Open work orders", open.Count, "number", $"{critical.Count} critical", 100, "/real-estate/work-orders"));
            foreach (var w in critical.Take(5)) alerts.Add(new("critical", "Maintenance", "action", $"Critical work order: {w.Issue}.", "/real-estate/work-orders", 20));
        }

        return new DashboardContribution(kpis, charts, alerts);
    }
}
