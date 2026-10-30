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

/// <summary>
/// The overview period: by default the last year, i.e. the 12 months ending today inclusive (today minus 1 year plus 1 day, up to today),
/// so presets never overlap the previous period by a day. At most <see cref="MaxMonths"/> months.
/// </summary>
public static class DashboardRange
{
    /// <summary>Longest period the overview accepts: 5 years (the picker uses the same limit).</summary>
    public const int MaxMonths = 60;

    /// <summary>Start of the default one-year period ending on <paramref name="to"/>.</summary>
    public static DateOnly DefaultFrom(DateOnly to) => StartFor(to, 12);

    /// <summary>Start of a period of <paramref name="months"/> months ending on <paramref name="to"/> inclusive.</summary>
    public static DateOnly StartFor(DateOnly to, int months) => to.AddMonths(-months).AddDays(1);

    /// <summary>True when <paramref name="from"/>..<paramref name="to"/> is no longer than <see cref="MaxMonths"/> months.</summary>
    public static bool WithinMax(DateOnly from, DateOnly to) => from >= StartFor(to, MaxMonths);

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

    /// <summary>Chart bucket size for a period: weeks up to 3 months, months up to 2 years, quarters beyond.</summary>
    public static string Granularity(DateOnly from, DateOnly to) =>
        from >= StartFor(to, 3) ? "week" : from >= StartFor(to, 24) ? "month" : "quarter";

    /// <summary>
    /// Chart buckets, oldest first, counted back from <paramref name="to"/> so every bucket is a whole week, month or quarter
    /// (1Y = 12 months, 2Y = 24 months, 3Y = 12 quarters, 3M = 13 weeks). A leading remainder shorter than half a bucket is merged
    /// into the first bucket instead of showing as a stub bar; a longer remainder stays as its own (partial) bucket.
    /// </summary>
    public static IReadOnlyList<DashboardBucket> Buckets(DateOnly from, DateOnly to, string granularity)
    {
        if (to < from) return [];
        var list = new List<DashboardBucket>();
        // a period ending on a month end keeps month-end boundaries (30 Sep -> 31 Aug -> 31 Jul ...), i.e. calendar months
        var monthEnd = to.Day == DateTime.DaysInMonth(to.Year, to.Month);
        DateOnly EndOf(int i)
        {
            if (granularity == "week") return to.AddDays(-7 * i);
            var e = to.AddMonths(-(granularity == "quarter" ? 3 : 1) * i);
            return monthEnd ? new DateOnly(e.Year, e.Month, DateTime.DaysInMonth(e.Year, e.Month)) : e;
        }
        for (var i = 0; ; i++)
        {
            var end = EndOf(i);
            var start = EndOf(i + 1).AddDays(1);
            if (start <= from) { list.Add(new(from, end)); break; }
            list.Add(new(start, end));
        }
        list.Reverse();
        if (list.Count > 1)
        {
            var first = list[0]; var next = list[1];
            var full = next.To.DayNumber - next.From.DayNumber + 1;
            if (first.To.DayNumber - first.From.DayNumber + 1 < full / 2.0) { list[1] = next with { From = first.From }; list.RemoveAt(0); }
        }
        return list;
    }

    /// <summary>English fallback axis label for a bucket (the web client formats its own from <see cref="DashboardBucket"/>).</summary>
    public static string Label(DashboardBucket b, string granularity) => granularity switch
    {
        "week" => b.From.ToString("dd MMM", System.Globalization.CultureInfo.InvariantCulture),
        "quarter" => $"{b.From.AddDays(14):MMM}–{b.To.AddDays(-14):MMM yy}",
        _ => b.From.AddDays((b.To.DayNumber - b.From.DayNumber) / 2).ToString("MMM yy", System.Globalization.CultureInfo.InvariantCulture),
    };
}

/// <summary>One chart bucket, <c>From</c>..<c>To</c> inclusive.</summary>
public sealed record DashboardBucket(DateOnly From, DateOnly To);

/// <summary>
/// A headline number. <c>Format</c> is "money", "percent" or "number"; <c>Caption</c> is the small line under it (English, fully resolved).
/// When <c>CaptionKey</c> is set the client may translate the caption itself from that key, filling <c>{name}</c> placeholders from
/// <c>CaptionArgs</c> (yyyy-MM-dd values are formatted as dates in the user's language).
/// </summary>
public sealed record KpiCard(string Key, string Label, decimal Value, string Format, string? Caption = null, int Order = 0, string? Link = null,
    string? CaptionKey = null, IReadOnlyDictionary<string, string>? CaptionArgs = null);

public sealed record ChartSeries(string Name, IReadOnlyList<decimal> Values);
/// <summary>
/// <c>Kind</c> is "bar" (grouped), "stacked" or "hbar" (horizontal). Categories label the x axis (or the rows of an hbar).
/// Time-series charts also send <c>Buckets</c> (one per category) and <c>Granularity</c> ("week" | "month" | "quarter") so the client can
/// label the axis in the user's language.
/// </summary>
public sealed record DashboardChart(string Key, string Title, string Kind, IReadOnlyList<string> Categories, IReadOnlyList<ChartSeries> Series, string? Subtitle = null, int Order = 0,
    IReadOnlyList<DashboardBucket>? Buckets = null, string? Granularity = null);

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
    public GetDashboardValidator(IClock clock)
    {
        // "today" in UTC; one extra day lets clients ahead of UTC (e.g. Asia/Qatar, Asia/Karachi just after midnight) send their own today.
        DateOnly Today() => DateOnly.FromDateTime(clock.UtcNow);
        RuleFor(x => x.From).Must(f => f is null || f.Value != default).WithMessage("Enter a valid start date.");
        RuleFor(x => x.To).Must(t => t is null || t.Value != default).WithMessage("Enter a valid end date.");
        RuleFor(x => x).Must(x => x.From is not { } f || x.To is not { } t || f <= t).WithName("To").WithMessage("The end date must be on or after the start date.");
        RuleFor(x => x.To).Must(t => t is null || t.Value == default || t.Value <= Today().AddDays(1)).WithMessage("The end date can't be in the future.");
        RuleFor(x => x).Must(x => x.From is not { } f || f == default || DashboardRange.WithinMax(f, x.To ?? Today()))
            .WithName("From").WithMessage("The date range can be at most 5 years.");
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
