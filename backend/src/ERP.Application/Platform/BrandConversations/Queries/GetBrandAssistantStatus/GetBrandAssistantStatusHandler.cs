using ERP.Application.Common;

namespace ERP.Application.Platform;

internal sealed class GetBrandAssistantStatusHandler(PlatformSettingsStore settingsStore) : IQueryHandler<GetBrandAssistantStatusQuery, BrandAssistantStatus>
{
    public async Task<BrandAssistantStatus> Handle(GetBrandAssistantStatusQuery query, CancellationToken ct)
    {
        var s = await settingsStore.GetAsync(ct);
        return new(s.BrandAssistantEnabled, s.SupportEmail);
    }
}
