using ERP.Application.Common;

namespace ERP.Application.Platform;

internal sealed class SetAssistantVisibilityHandler(AssistantConsole console, IAiProviderRepository providers, PlatformSettingsStore settingsStore, PlatformLog log)
    : ICommandHandler<SetAssistantVisibilityCommand, AssistantConsoleView>
{
    public async Task<AssistantConsoleView> Handle(SetAssistantVisibilityCommand command, CancellationToken ct)
    {
        await console.RequireOwnerAsync(ct);
        var s = await settingsStore.GetTrackedAsync(ct);
        if (command.Input.BrandEnabled && !s.BrandAssistantEnabled && !await providers.AnyAsync(p => p.UseForBrand, ct))
            throw new FieldValidationException("brandEnabled", "Connect a service for the public assistant first.");
        if (command.Input.WorkspaceEnabled && !s.WorkspaceAssistantEnabled && !await providers.AnyAsync(p => p.UseForWorkspace, ct))
            throw new FieldValidationException("workspaceEnabled", "Clear a service for client data first.");
        if (s.BrandAssistantEnabled != command.Input.BrandEnabled) log.Add(command.Input.BrandEnabled ? "Public AI assistant shown" : "Public AI assistant hidden", null, null);
        if (s.WorkspaceAssistantEnabled != command.Input.WorkspaceEnabled) log.Add(command.Input.WorkspaceEnabled ? "Workspace AI assistant shown" : "Workspace AI assistant hidden", null, null);
        s.BrandAssistantEnabled = command.Input.BrandEnabled;
        s.WorkspaceAssistantEnabled = command.Input.WorkspaceEnabled;
        await providers.SaveChangesAsync(ct);
        return await console.ViewAsync(ct);
    }
}
