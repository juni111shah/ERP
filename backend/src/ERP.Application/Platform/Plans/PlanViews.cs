using ERP.Application.Abstractions;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

/// <summary>Builds a plan's console view (with its client count) and copies the console's input onto a plan.</summary>
internal sealed class PlanViews(IReadRepository<WorkspaceSubscription> subscriptions, PlatformSettingsStore settingsStore)
{
    public async Task<PlanView> ViewAsync(SubscriptionPlan p, CancellationToken ct)
    {
        var settings = await settingsStore.GetAsync(ct);
        return ToView(p, settings.Currency, await subscriptions.CountAsync(s => s.PlanId == p.Id && s.Status != SubscriptionStatus.Cancelled, ct));
    }

    public static PlanView ToView(SubscriptionPlan p, string currency, int clients) => new(
        p.Id, p.Code, p.Name, p.Description, p.IsPublic, p.IsActive, p.IsRecommended, p.SortOrder, p.MonthlyPrice, p.AnnualPrice, currency, p.TrialDays, p.MaxUsers, p.MaxLegalEntities, p.ModuleList(), p.FeatureList(), clients);

    public static void Apply(SubscriptionPlan p, PlanInput i)
    {
        p.Code = i.Code; p.Name = i.Name.Trim(); p.Description = i.Description?.Trim(); p.IsPublic = i.IsPublic; p.IsRecommended = i.IsRecommended; p.SortOrder = i.SortOrder;
        p.MonthlyPrice = i.MonthlyPrice; p.AnnualPrice = i.AnnualPrice; p.TrialDays = i.TrialDays; p.MaxUsers = i.MaxUsers; p.MaxLegalEntities = i.MaxLegalEntities;
        p.ModuleKeys = i.Modules is { Count: > 0 } m ? string.Join(",", m.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct()) : null;
        p.Features = i.Features is { Count: > 0 } f ? string.Join("\n", f.Select(x => x.Trim()).Where(x => x.Length > 0)) : null;
    }
}
