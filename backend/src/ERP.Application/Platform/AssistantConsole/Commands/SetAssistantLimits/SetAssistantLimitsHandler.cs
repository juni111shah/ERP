using ERP.Application.Common;

namespace ERP.Application.Platform;

internal sealed class SetAssistantLimitsHandler(AssistantConsole console, IAiProviderRepository providers, PlatformSettingsStore settingsStore, PlatformLog log)
    : ICommandHandler<SetAssistantLimitsCommand, AssistantConsoleView>
{
    public async Task<AssistantConsoleView> Handle(SetAssistantLimitsCommand command, CancellationToken ct)
    {
        await console.RequireOwnerAsync(ct);
        var s = await settingsStore.GetTrackedAsync(ct);
        s.BrandAssistantDailyTokens = command.Input.BrandDailyTokens;
        s.WorkspaceAssistantDailyTokens = command.Input.WorkspaceDailyTokens;
        log.Add("AI assistant limits changed", null, null, $"Public {command.Input.BrandDailyTokens:N0} and workspace {command.Input.WorkspaceDailyTokens:N0} tokens a day.");
        await providers.SaveChangesAsync(ct);
        return await console.ViewAsync(ct);
    }
}
