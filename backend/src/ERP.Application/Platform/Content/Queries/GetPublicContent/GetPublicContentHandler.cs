using ERP.Application.Common;

namespace ERP.Application.Platform;

internal sealed class GetPublicContentHandler(ISubscriptionPlanRepository plans, PlatformSettingsStore settingsStore) : IQueryHandler<GetPublicContentQuery, PublicContent>
{
    public async Task<PublicContent> Handle(GetPublicContentQuery query, CancellationToken ct)
    {
        var settings = await settingsStore.GetAsync(ct);
        var trialDays = await plans.LongestPublicTrialAsync(ct) ?? 0;
        return new(PublicContentText.BuiltIn, PublicContentText.Trust, PublicContentText.Faqs(trialDays, settings.SupportEmail), string.IsNullOrWhiteSpace(settings.AboutText) ? null : settings.AboutText.Trim());
    }
}
