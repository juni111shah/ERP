using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

internal sealed class GetPlatformDashboardHandler(
    ClientReader clients, ISubscriptionInvoiceRepository invoices, ISubscriptionPlanRepository plans, IReadRepository<SignupRequest> signups, IPlatformActivityRepository activity,
    PlatformSettingsStore settingsStore, IClock clock) : IQueryHandler<GetPlatformDashboardQuery, PlatformDashboard>
{
    public async Task<PlatformDashboard> Handle(GetPlatformDashboardQuery query, CancellationToken ct)
    {
        // 1. Every client and every invoice that counts.
        var settings = await settingsStore.GetAsync(ct);
        var rows = await clients.RowsAsync(null, ct);
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var allInvoices = await invoices.ListAmountsNotVoidAsync(ct);
        var paid = allInvoices.Where(i => i.Status == SubscriptionInvoiceStatus.Paid && i.PaidOn is not null).ToList();
        var open = allInvoices.Where(i => i.Status == SubscriptionInvoiceStatus.Open).ToList();

        // 2. The figures.
        bool Paying(ClientRow r) => r.Status is SubscriptionStatus.Active or SubscriptionStatus.PastDue && !r.IsComplimentary;
        var mrr = rows.Where(Paying).Sum(r => r.MonthlyValue);
        var months = Enumerable.Range(0, 6).Select(n => new DateOnly(today.Year, today.Month, 1).AddMonths(-5 + n)).ToList();
        var since = clock.UtcNow.AddDays(-30);
        var planNames = await plans.NamesByIdAsync(ct);
        return new(
            settings.Currency, mrr, mrr * 12, rows.Count, rows.Count(r => r.Status == SubscriptionStatus.Trialing), rows.Count(r => r.Status == SubscriptionStatus.Active), rows.Count(r => r.Status == SubscriptionStatus.PastDue),
            rows.Count(r => r.Status == SubscriptionStatus.Expired), rows.Count(r => r.Status == SubscriptionStatus.Suspended), rows.Count(r => r.Status == SubscriptionStatus.Cancelled), rows.Count(r => r.Status is null),
            open.Sum(i => i.Total), open.Where(i => i.DueOn < today).Sum(i => i.Total), paid.Where(i => i.PaidOn!.Value.Year == today.Year && i.PaidOn!.Value.Month == today.Month).Sum(i => i.Total), paid.Sum(i => i.Total),
            await signups.CountAsync(r => r.Status == SignupStatus.PendingReview, ct), rows.Count(r => r.CreatedAt >= since), rows.Count(r => r.HasPlanRequest),
            rows.Where(r => r.PlanId is not null && r.Status != SubscriptionStatus.Cancelled).GroupBy(r => r.PlanId!.Value).Select(g => new PlanCount(g.Key, planNames.GetValueOrDefault(g.Key, "Plan"), g.Count(), g.Where(Paying).Sum(r => r.MonthlyValue))).OrderByDescending(p => p.Clients).ToList(),
            months.Select(m => new MonthAmount(m.ToString("yyyy-MM"), paid.Where(i => i.PaidOn!.Value.Year == m.Year && i.PaidOn!.Value.Month == m.Month).Sum(i => i.Total))).ToList(),
            rows.Where(r => r.Status == SubscriptionStatus.Trialing && r.TrialEndsOn is { } e && e.DayNumber - today.DayNumber <= 7).OrderBy(r => r.TrialEndsOn).Take(10).Select(r => new ExpiringTrial(r.OrgId, r.Name, r.TrialEndsOn!.Value, r.TrialEndsOn!.Value.DayNumber - today.DayNumber)).ToList(),
            (await activity.ListLatestAsync(10, ct)).Select(ActivityRow.From).ToList());
    }
}
