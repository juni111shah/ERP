using ERP.Application.Common;

namespace ERP.Application.Platform;

internal sealed class DeleteAiProviderHandler(AssistantConsole console, IAiProviderRepository providers, PlatformLog log) : ICommandHandler<DeleteAiProviderCommand, AssistantConsoleView>
{
    public async Task<AssistantConsoleView> Handle(DeleteAiProviderCommand command, CancellationToken ct)
    {
        await console.RequireOwnerAsync(ct);
        var provider = await providers.GetForUpdateAsync(command.Id, ct);
        providers.Remove(provider);
        log.Add("AI service deleted", null, null, provider.Name);
        await providers.SaveChangesAsync(ct);
        return await console.ViewAsync(ct);
    }
}
