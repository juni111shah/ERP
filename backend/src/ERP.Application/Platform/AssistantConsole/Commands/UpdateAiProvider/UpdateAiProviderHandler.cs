using ERP.Application.Common;

namespace ERP.Application.Platform;

internal sealed class UpdateAiProviderHandler(AssistantConsole console, IAiProviderRepository providers, PlatformLog log) : ICommandHandler<UpdateAiProviderCommand, AssistantConsoleView>
{
    public async Task<AssistantConsoleView> Handle(UpdateAiProviderCommand command, CancellationToken ct)
    {
        await console.RequireOwnerAsync(ct);
        var provider = await providers.GetForUpdateAsync(command.Id, ct);
        var newKey = !string.IsNullOrWhiteSpace(command.Input.ApiKey);
        var addressChanged = !string.Equals(provider.BaseUrl, AiEndpointRules.Normalize(command.Input.BaseUrl), StringComparison.OrdinalIgnoreCase);
        // Otherwise someone could point a stored key at a different address.
        if (addressChanged && !newKey) throw new FieldValidationException("apiKey", "The address changed, so paste the key again.");
        var connectionChanged = addressChanged || newKey || provider.Model != command.Input.Model.Trim();
        AssistantConsole.Apply(provider, command.Input);
        if (newKey) console.SetKey(provider, command.Input.ApiKey!);
        if (connectionChanged) { provider.LastTestedAt = null; provider.LastTestOk = null; provider.LastTestMessage = null; }
        log.Add(newKey ? "AI service key replaced" : "AI service changed", null, null, $"{provider.Name}, model {provider.Model}.");
        await providers.SaveChangesAsync(ct);
        return await console.ViewAsync(ct);
    }
}
