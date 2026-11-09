using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Application.Dashboard;

internal sealed class GetDashboardHandler(IEnumerable<IDashboardContributor> contributors, IModuleAccess modules, IClock clock) : IQueryHandler<GetDashboardQuery, DashboardDto>
{
    public async Task<DashboardDto> Handle(GetDashboardQuery q, CancellationToken ct)
    {
        // 1. The period (defaults filled in) and the scope every contributor builds for.
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var (from, to) = DashboardRange.Resolve(q.From, q.To, today);
        var scope = new DashboardScope(q.LegalEntityId, today) { From = from, To = to };

        // 2. Core and every enabled module add their cards, charts and alerts.
        var kpis = new List<KpiCard>(); var charts = new List<DashboardChart>(); var alerts = new List<DashboardAlert>();
        foreach (var c in contributors)
        {
            if (c.ModuleKey != "core" && !await modules.IsEnabledAsync(c.ModuleKey, ct)) continue;
            var part = await c.BuildAsync(scope, ct);
            kpis.AddRange(part.Kpis); charts.AddRange(part.Charts); alerts.AddRange(part.Alerts);
        }

        // 3. In their order; alerts that need action first.
        return new DashboardDto(scope.Today, kpis.OrderBy(k => k.Order).ToList(), charts.OrderBy(c => c.Order).ToList(),
            alerts.OrderBy(a => a.Urgency == "action" ? 0 : a.Urgency == "upcoming" ? 1 : 2).ThenBy(a => a.Rank).ToList(), scope.From, scope.To);
    }
}
