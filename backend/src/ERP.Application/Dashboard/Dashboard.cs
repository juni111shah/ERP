using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using FluentValidation;
using MediatR;

namespace ERP.Application.Dashboard;

/// <summary>
/// What a dashboard is built for: the legal entity in scope (null = everything the caller may see), today's date and the reporting
/// period <see cref="From"/>..<see cref="To"/> (inclusive). Period figures (billed, collected, result, expenses) use the period;
/// balances and open items (receivables, payables, cheques, approvals, module status counts) stay as of today unless a contributor says otherwise.
/// </summary>
public sealed record DashboardScope(Guid? LegalEntityId, DateOnly Today)
{
    public DateOnly From { get; init; } = DashboardRange.DefaultFrom(Today);
    public DateOnly To { get; init; } = Today;
}

/// <summary>The overview period: by default the last year (today minus 12 months up to today).</summary>
public static class DashboardRange
{
    public static DateOnly DefaultFrom(DateOnly to) => to.AddYears(-1);

    /// <summary>Fills a missing end with today and a missing start with one year before the end.</summary>
    public static (DateOnly From, DateOnly To) Resolve(DateOnly? from, DateOnly? to, DateOnly today)
    {
        var end = to ?? today;
        return (from ?? DefaultFrom(end), end);
    }

    /// <summary>First day of each calendar month touched by the period, oldest first.</summary>
    public static IReadOnlyList<DateOnly> Months(DateOnly from, DateOnly to)
    {
        var list = new List<DateOnly>();
        for (var m = new DateOnly(from.Year, from.Month, 1); m <= to; m = m.AddMonths(1)) list.Add(m);
        return list;
    }
}

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

/// <summary><c>From</c>/<c>To</c> echo the reporting period the figures were built for.</summary>
public sealed record DashboardDto(DateOnly AsOf, IReadOnlyList<KpiCard> Kpis, IReadOnlyList<DashboardChart> Charts, IReadOnlyList<DashboardAlert> Alerts, DateOnly From, DateOnly To);

/// <summary>
/// Core and every module add their own cards, charts and alerts to the overview. The overview never knows an industry:
/// a contributor is consulted only when its module is enabled for the workspace ("core" always is).
/// </summary>
public interface IDashboardContributor
{
    string ModuleKey { get; }
    Task<DashboardContribution> BuildAsync(DashboardScope scope, CancellationToken ct);
}

/// <summary><c>From</c>/<c>To</c> are optional; missing values default to the last year ending today.</summary>
public sealed record GetDashboard(Guid? LegalEntityId, DateOnly? From = null, DateOnly? To = null) : IQuery<DashboardDto>;

public sealed class GetDashboardValidator : AbstractValidator<GetDashboard>
{
    public GetDashboardValidator()
    {
        RuleFor(x => x.From).Must(f => f is null || f.Value != default).WithMessage("Enter a valid start date.");
        RuleFor(x => x.To).Must(t => t is null || t.Value != default).WithMessage("Enter a valid end date.");
        RuleFor(x => x).Must(x => x.From is not { } f || x.To is not { } t || f <= t).WithName("To").WithMessage("The end date must be on or after the start date.");
        RuleFor(x => x).Must(x => x.From is not { } f || (x.To ?? DateOnly.FromDateTime(DateTime.UtcNow)).DayNumber - f.DayNumber <= PartyLedger.MaxDays)
            .WithName("From").WithMessage("The date range can be at most 10 years.");
    }
}

internal sealed class DashboardHandler(IEnumerable<IDashboardContributor> contributors, IModuleAccess modules, IClock clock) : IRequestHandler<GetDashboard, DashboardDto>
{
    public async Task<DashboardDto> Handle(GetDashboard q, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var (from, to) = DashboardRange.Resolve(q.From, q.To, today);
        var scope = new DashboardScope(q.LegalEntityId, today) { From = from, To = to };
        var kpis = new List<KpiCard>(); var charts = new List<DashboardChart>(); var alerts = new List<DashboardAlert>();
        foreach (var c in contributors)
        {
            if (c.ModuleKey != "core" && !await modules.IsEnabledAsync(c.ModuleKey, ct)) continue;
            var part = await c.BuildAsync(scope, ct);
            kpis.AddRange(part.Kpis); charts.AddRange(part.Charts); alerts.AddRange(part.Alerts);
        }
        return new DashboardDto(scope.Today, kpis.OrderBy(k => k.Order).ToList(), charts.OrderBy(c => c.Order).ToList(),
            alerts.OrderBy(a => a.Urgency == "action" ? 0 : a.Urgency == "upcoming" ? 1 : 2).ThenBy(a => a.Rank).ToList(), scope.From, scope.To);
    }
}
