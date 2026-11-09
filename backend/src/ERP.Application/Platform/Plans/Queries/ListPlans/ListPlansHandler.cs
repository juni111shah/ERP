using ERP.Application.Common;

namespace ERP.Application.Platform;

internal sealed class ListPlansHandler(ISubscriptionPlanRepository plans, IWorkspaceSubscriptionRepository subscriptions, PlatformSettingsStore settingsStore)
    : IQueryHandler<ListPlansQuery, IReadOnlyList<PlanView>>
{
    public async Task<IReadOnlyList<PlanView>> Handle(ListPlansQuery query, CancellationToken ct)
    {
        var settings = await settingsStore.GetAsync(ct);
        var counts = await subscriptions.CountRunningByPlanAsync(ct);
        var rows = await plans.ListInOrderAsync(ct);
        return rows.Where(p => query.IncludeArchived || p.IsActive).Select(p => PlanViews.ToView(p, settings.Currency, counts.GetValueOrDefault(p.Id))).ToList();
    }
}
