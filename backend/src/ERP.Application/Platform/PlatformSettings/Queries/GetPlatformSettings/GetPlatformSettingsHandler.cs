using ERP.Application.Common;

namespace ERP.Application.Platform;

internal sealed class GetPlatformSettingsHandler(PlatformSettingsStore settingsStore, IPlatformMailer mailer) : IQueryHandler<GetPlatformSettingsQuery, PlatformSettingsView>
{
    public async Task<PlatformSettingsView> Handle(GetPlatformSettingsQuery query, CancellationToken ct) => PlatformSettingsView.From(await settingsStore.GetAsync(ct), mailer.IsEnabled);
}
