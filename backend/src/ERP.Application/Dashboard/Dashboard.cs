using ERP.Application.Abstractions;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Dashboard;

/// <summary>What a dashboard is built for: the legal entity in scope (null = everything the caller may see) and today's date.</summary>
public sealed record DashboardScope(Guid? LegalEntityId, DateOnly Today);

/// <summary>A headline number. <c>Format</c> is "money", "percent" or "number"; <c>Caption</c> is the small line under it.</summary>
public sealed record KpiCard(string Key, string Label, decimal Value, string Format, string? Caption = null, int Order = 0, string? Link = null);

public sealed record ChartSeries(string Name, IReadOnlyList<decimal> Values);
/// <summary><c>Kind</c> is "bar" (grouped), "stacked" or "hbar" (horizontal). Categories label the x axis (or the rows of an hbar).</summary>
public sealed record DashboardChart(string Key, string Title, string Kind, IReadOnlyList<string> Categories, IReadOnlyList<ChartSeries> Series, string? Subtitle = null, int Order = 0);

/// <summary>Severity: "critical" | "warning" | "info". Urgency: "action" | "upcoming" | "info".</summary>
public sealed record DashboardAlert(string Severity, string Category, string Urgency, string Message, string? Link = null, int Rank = 0);

public sealed record DashboardContribution(IReadOnlyList<KpiCard> Kpis, IReadOnlyList<DashboardChart> Charts, IReadOnlyList<DashboardAlert> Alerts)
{
    public static DashboardContribution Empty { get; } = new([], [], []);
}

public sealed record DashboardDto(DateOnly AsOf, IReadOnlyList<KpiCard> Kpis, IReadOnlyList<DashboardChart> Charts, IReadOnlyList<DashboardAlert> Alerts);

/// <summary>
/// Core and every module add their own cards, charts and alerts to the overview. The overview never knows an industry:
/// a contributor is consulted only when its module is enabled for the workspace ("core" always is).
/// </summary>
public interface IDashboardContributor
{
    string ModuleKey { get; }
    Task<DashboardContribution> BuildAsync(DashboardScope scope, CancellationToken ct);
}

public sealed record GetDashboard(Guid? LegalEntityId) : IQuery<DashboardDto>;

internal sealed class DashboardHandler(IEnumerable<IDashboardContributor> contributors, IModuleAccess modules, IClock clock) : IRequestHandler<GetDashboard, DashboardDto>
{
    public async Task<DashboardDto> Handle(GetDashboard q, CancellationToken ct)
    {
        var scope = new DashboardScope(q.LegalEntityId, DateOnly.FromDateTime(clock.UtcNow));
        var kpis = new List<KpiCard>(); var charts = new List<DashboardChart>(); var alerts = new List<DashboardAlert>();
        foreach (var c in contributors)
        {
            if (c.ModuleKey != "core" && !await modules.IsEnabledAsync(c.ModuleKey, ct)) continue;
            var part = await c.BuildAsync(scope, ct);
            kpis.AddRange(part.Kpis); charts.AddRange(part.Charts); alerts.AddRange(part.Alerts);
        }
        return new DashboardDto(scope.Today, kpis.OrderBy(k => k.Order).ToList(), charts.OrderBy(c => c.Order).ToList(),
            alerts.OrderBy(a => a.Urgency == "action" ? 0 : a.Urgency == "upcoming" ? 1 : 2).ThenBy(a => a.Rank).ToList());
    }
}
