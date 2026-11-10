using ERP.Application.Abstractions;
using ERP.Application.Assistant;
using ERP.Application.Common;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

/// <summary>The founder's control of the AI assistants. Anyone on the platform team can look; only the owner can change anything or use a key.</summary>
internal sealed class AssistantConsole(
    IAiProviderRepository providers, PlatformSettingsStore settingsStore, IPlatformDirectory directory, IAiKeyProtector protector, IAssistantUsageStore usage, ICurrentUser user, IClock clock)
{
    public async Task<bool> IsOwnerAsync(CancellationToken ct) => user.UserId is { } id && await directory.IsPlatformOwnerAsync(id, ct);

    public async Task RequireOwnerAsync(CancellationToken ct)
    {
        if (!await IsOwnerAsync(ct)) throw new ForbiddenException("Only the platform owner can change the AI assistant.");
    }

    public async Task<AssistantConsoleView> ViewAsync(CancellationToken ct)
    {
        var settings = await settingsStore.GetAsync(ct);
        var rows = await providers.ListInOrderAsync(ct);
        return new(settings.BrandAssistantEnabled, settings.WorkspaceAssistantEnabled, await IsOwnerAsync(ct), rows.Select(p => new AiProviderView(
            p.Id, p.Name, p.BaseUrl, p.Model, p.ApiKeyHint, protector.TryUnprotect(p.EncryptedApiKey) is null, p.UseForBrand, p.UseForWorkspace, p.LastTestedAt, p.LastTestOk, p.LastTestMessage)).ToList(),
            settings.BrandAssistantDailyTokens, settings.WorkspaceAssistantDailyTokens, await usage.ListAsync(DateOnly.FromDateTime(clock.UtcNow), ct));
    }

    public static void Apply(AiProvider p, AiProviderInput i)
    {
        p.Name = i.Name.Trim();
        p.BaseUrl = AiEndpointRules.Normalize(i.BaseUrl);
        p.Model = i.Model.Trim();
        p.UseForBrand = i.UseForBrand;
        p.UseForWorkspace = i.UseForWorkspace;
    }

    public void SetKey(AiProvider p, string apiKey)
    {
        var key = apiKey.Trim();
        p.EncryptedApiKey = protector.Protect(key);
        p.ApiKeyHint = key[^4..];
    }
}
