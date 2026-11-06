using System.Net;
using System.Net.Sockets;
using ERP.Application.Abstractions;
using ERP.Application.Assistant;
using ERP.Application.Common;
using ERP.Domain.Platform;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Platform;

public sealed record AiProviderView(
    Guid Id, string Name, string BaseUrl, string Model, string KeyHint, bool KeyNeedsReentry, bool UseForBrand, bool UseForWorkspace,
    DateTime? LastTestedAt, bool? LastTestOk, string? LastTestMessage);

public sealed record AssistantConsoleView(
    bool BrandEnabled, bool WorkspaceEnabled, bool CanManage, IReadOnlyList<AiProviderView> Providers,
    int BrandDailyTokens, int WorkspaceDailyTokens, int WorkspaceDailyMessagesPerClient, IReadOnlyList<AssistantUsageRow> UsageToday, IReadOnlyList<AssistantUsageRow> UsageLast30Days);

/// <summary>ApiKey is write-only. Leave it empty when editing to keep the stored key.</summary>
public sealed record AiProviderInput(string Name, string BaseUrl, string Model, string? ApiKey, bool UseForBrand, bool UseForWorkspace);
public sealed record AssistantVisibilityInput(bool BrandEnabled, bool WorkspaceEnabled);
public sealed record AiProviderOrderInput(IReadOnlyList<Guid> Ids);
public sealed record AssistantLimitsInput(int BrandDailyTokens, int WorkspaceDailyTokens, int WorkspaceDailyMessagesPerClient);

public sealed record GetAssistantConsole : IQuery<AssistantConsoleView>;
public sealed record SetAssistantVisibility(AssistantVisibilityInput Input) : ICommand<AssistantConsoleView>;
public sealed record SetAssistantLimits(AssistantLimitsInput Input) : ICommand<AssistantConsoleView>;
public sealed record CreateAiProvider(AiProviderInput Input) : ICommand<AssistantConsoleView>;
public sealed record UpdateAiProvider(Guid Id, AiProviderInput Input) : ICommand<AssistantConsoleView>;
public sealed record DeleteAiProvider(Guid Id) : ICommand<AssistantConsoleView>;
public sealed record ReorderAiProviders(AiProviderOrderInput Input) : ICommand<AssistantConsoleView>;
public sealed record TestAiProvider(Guid Id) : ICommand<AssistantConsoleView>;

/// <summary>Encrypts the keys of AI services for storage (implemented in Infrastructure).</summary>
public interface IAiKeyProtector
{
    string Protect(string apiKey);
    /// <summary>Null when the key can no longer be read, for example after restoring a backup on another machine.</summary>
    string? TryUnprotect(string encrypted);
}

public sealed record AiConnectionResult(bool Ok, string Message);

/// <summary>Makes one small real call to an AI service. The message is safe to store and show: it never holds the key or the service's own reply.</summary>
public interface IAiConnectionTester
{
    Task<AiConnectionResult> TestAsync(string baseUrl, string model, string apiKey, CancellationToken ct = default);
}

/// <summary>Which addresses a key may be sent to: public HTTPS only, so a stored key can never reach a machine on the server's own network.</summary>
public static class AiEndpointRules
{
    public static string Normalize(string? url) => (url ?? "").Trim().TrimEnd('/');

    /// <summary>The problem with this address, or null when it is fine.</summary>
    public static string? Problem(string? url)
    {
        if (!Uri.TryCreate(Normalize(url), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return "Enter a full address that starts with https://.";
        if (!string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            return "Enter only the address, with no sign-in details or query.";
        var host = uri.IdnHost.TrimEnd('.');
        if (!host.Contains('.') || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".lan", StringComparison.OrdinalIgnoreCase))
            return "Use the service's public address.";
        if (IPAddress.TryParse(host.Trim('[', ']'), out var ip) && IsPrivate(ip)) return "Use the service's public address.";
        return null;
    }

    /// <summary>Loopback, private, link-local and similar ranges that a public service never uses.</summary>
    public static bool IsPrivate(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any)) return true;
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal || ip.IsIPv6Multicast;
        var b = ip.GetAddressBytes();
        return b[0] is 0 or 10 or >= 224 || (b[0] == 100 && b[1] >= 64 && b[1] <= 127) || (b[0] == 169 && b[1] == 254)
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168);
    }
}

public sealed class AiProviderInputValidator : AbstractValidator<AiProviderInput>
{
    public AiProviderInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Enter a name for this service.").MaximumLength(80);
        RuleFor(x => x.BaseUrl).NotEmpty().WithMessage("Enter the service's address.").MaximumLength(300);
        RuleFor(x => x.BaseUrl).Must(u => AiEndpointRules.Problem(u) is null).WithMessage(x => AiEndpointRules.Problem(x.BaseUrl) ?? "").When(x => !string.IsNullOrWhiteSpace(x.BaseUrl));
        RuleFor(x => x.Model).NotEmpty().WithMessage("Enter the model name.").MaximumLength(150);
        RuleFor(x => x.ApiKey).Must(k => k!.Trim().Length is >= 8 and <= 1000 && !k.Trim().Any(char.IsWhiteSpace)).WithMessage("That does not look like an API key.")
            .When(x => !string.IsNullOrWhiteSpace(x.ApiKey));
    }
}

public sealed class SetAssistantLimitsValidator : AbstractValidator<SetAssistantLimits>
{
    private const int Min = 1_000;
    private const int Max = 100_000_000;

    public SetAssistantLimitsValidator()
    {
        RuleFor(x => x.Input.BrandDailyTokens).InclusiveBetween(Min, Max).WithMessage("Enter a number from 1,000 to 100,000,000.");
        RuleFor(x => x.Input.WorkspaceDailyTokens).InclusiveBetween(Min, Max).WithMessage("Enter a number from 1,000 to 100,000,000.");
        RuleFor(x => x.Input.WorkspaceDailyMessagesPerClient).InclusiveBetween(1, 1_000_000).WithMessage("Enter a number from 1 to 1,000,000.");
    }
}

public sealed class CreateAiProviderValidator : AbstractValidator<CreateAiProvider>
{
    public CreateAiProviderValidator()
    {
        RuleFor(x => x.Input).SetValidator(new AiProviderInputValidator());
        RuleFor(x => x.Input.ApiKey).NotEmpty().WithMessage("Paste the API key.");
    }
}

public sealed class UpdateAiProviderValidator : AbstractValidator<UpdateAiProvider>
{
    public UpdateAiProviderValidator() => RuleFor(x => x.Input).SetValidator(new AiProviderInputValidator());
}

/// <summary>The founder's control of the AI assistants. Anyone on the platform team can look; only the owner can change anything or use a key.</summary>
internal sealed class AssistantConsoleHandlers(
    IRepository<AiProvider> providers, PlatformSettingsStore settingsStore, IPlatformDirectory directory, IAiKeyProtector protector, IAiConnectionTester tester,
    IAssistantUsageStore usage, PlatformLog log, ICurrentUser user, IClock clock) :
    IRequestHandler<GetAssistantConsole, AssistantConsoleView>,
    IRequestHandler<SetAssistantVisibility, AssistantConsoleView>,
    IRequestHandler<SetAssistantLimits, AssistantConsoleView>,
    IRequestHandler<CreateAiProvider, AssistantConsoleView>,
    IRequestHandler<UpdateAiProvider, AssistantConsoleView>,
    IRequestHandler<DeleteAiProvider, AssistantConsoleView>,
    IRequestHandler<ReorderAiProviders, AssistantConsoleView>,
    IRequestHandler<TestAiProvider, AssistantConsoleView>
{
    private async Task<bool> IsOwnerAsync(CancellationToken ct) => user.UserId is { } id && await directory.IsPlatformOwnerAsync(id, ct);

    private async Task RequireOwnerAsync(CancellationToken ct)
    {
        if (!await IsOwnerAsync(ct)) throw new ForbiddenException("Only the platform owner can change the AI assistant.");
    }

    private async Task<AssistantConsoleView> ViewAsync(CancellationToken ct)
    {
        var settings = await settingsStore.GetAsync(ct);
        var rows = await providers.Query().OrderBy(p => p.SortOrder).ThenBy(p => p.CreatedAt).ToListAsync(ct);
        var today = DateOnly.FromDateTime(clock.UtcNow);
        return new(settings.BrandAssistantEnabled, settings.WorkspaceAssistantEnabled, await IsOwnerAsync(ct), rows.Select(p => new AiProviderView(
            p.Id, p.Name, p.BaseUrl, p.Model, p.ApiKeyHint, protector.TryUnprotect(p.EncryptedApiKey) is null, p.UseForBrand, p.UseForWorkspace, p.LastTestedAt, p.LastTestOk, p.LastTestMessage)).ToList(),
            settings.BrandAssistantDailyTokens, settings.WorkspaceAssistantDailyTokens, settings.WorkspaceAssistantDailyMessagesPerClient, await usage.ListAsync(today, ct), await usage.TotalsSinceAsync(today.AddDays(-29), ct));
    }

    public async Task<AssistantConsoleView> Handle(SetAssistantLimits cmd, CancellationToken ct)
    {
        await RequireOwnerAsync(ct);
        var s = await settingsStore.GetTrackedAsync(ct);
        s.BrandAssistantDailyTokens = cmd.Input.BrandDailyTokens;
        s.WorkspaceAssistantDailyTokens = cmd.Input.WorkspaceDailyTokens;
        s.WorkspaceAssistantDailyMessagesPerClient = cmd.Input.WorkspaceDailyMessagesPerClient;
        log.Add("AI assistant limits changed", null, null, $"Public {cmd.Input.BrandDailyTokens:N0} and workspace {cmd.Input.WorkspaceDailyTokens:N0} tokens a day, {cmd.Input.WorkspaceDailyMessagesPerClient:N0} workspace messages a day per client.");
        await providers.SaveChangesAsync(ct);
        return await ViewAsync(ct);
    }

    public Task<AssistantConsoleView> Handle(GetAssistantConsole q, CancellationToken ct) => ViewAsync(ct);

    public async Task<AssistantConsoleView> Handle(SetAssistantVisibility cmd, CancellationToken ct)
    {
        await RequireOwnerAsync(ct);
        var s = await settingsStore.GetTrackedAsync(ct);
        if (cmd.Input.BrandEnabled && !s.BrandAssistantEnabled && !await providers.AnyAsync(p => p.UseForBrand, ct))
            throw new FieldValidationException("brandEnabled", "Connect a service for the public assistant first.");
        if (cmd.Input.WorkspaceEnabled && !s.WorkspaceAssistantEnabled && !await providers.AnyAsync(p => p.UseForWorkspace, ct))
            throw new FieldValidationException("workspaceEnabled", "Clear a service for client data first.");
        if (s.BrandAssistantEnabled != cmd.Input.BrandEnabled) log.Add(cmd.Input.BrandEnabled ? "Public AI assistant shown" : "Public AI assistant hidden", null, null);
        if (s.WorkspaceAssistantEnabled != cmd.Input.WorkspaceEnabled) log.Add(cmd.Input.WorkspaceEnabled ? "Workspace AI assistant shown" : "Workspace AI assistant hidden", null, null);
        s.BrandAssistantEnabled = cmd.Input.BrandEnabled;
        s.WorkspaceAssistantEnabled = cmd.Input.WorkspaceEnabled;
        await providers.SaveChangesAsync(ct);
        return await ViewAsync(ct);
    }

    public async Task<AssistantConsoleView> Handle(CreateAiProvider cmd, CancellationToken ct)
    {
        await RequireOwnerAsync(ct);
        var last = await providers.Query().OrderByDescending(p => p.SortOrder).Select(p => (int?)p.SortOrder).FirstOrDefaultAsync(ct);
        var provider = new AiProvider { SortOrder = (last ?? 0) + 10 };
        Apply(provider, cmd.Input);
        // Nothing answers through a new service until a test has passed and the founder has turned it on.
        provider.UseForBrand = false;
        provider.UseForWorkspace = false;
        SetKey(provider, cmd.Input.ApiKey!);
        providers.Add(provider);
        log.Add("AI service added", null, null, $"{provider.Name}, model {provider.Model}.");
        await providers.SaveChangesAsync(ct);
        return await ViewAsync(ct);
    }

    public async Task<AssistantConsoleView> Handle(UpdateAiProvider cmd, CancellationToken ct)
    {
        await RequireOwnerAsync(ct);
        var provider = await providers.GetForUpdateAsync(cmd.Id, ct);
        var newKey = !string.IsNullOrWhiteSpace(cmd.Input.ApiKey);
        var addressChanged = !string.Equals(provider.BaseUrl, AiEndpointRules.Normalize(cmd.Input.BaseUrl), StringComparison.OrdinalIgnoreCase);
        // Otherwise someone could point a stored key at a different address.
        if (addressChanged && !newKey) throw new FieldValidationException("apiKey", "The address changed, so paste the key again.");
        var connectionChanged = addressChanged || newKey || provider.Model != cmd.Input.Model.Trim();
        if (!connectionChanged && provider.LastTestOk != true)
        {
            if (cmd.Input.UseForBrand && !provider.UseForBrand) throw new FieldValidationException("useForBrand", "Press Test first. This can be turned on once a test has passed.");
            if (cmd.Input.UseForWorkspace && !provider.UseForWorkspace) throw new FieldValidationException("useForWorkspace", "Press Test first. This can be turned on once a test has passed.");
        }
        Apply(provider, cmd.Input);
        if (newKey) SetKey(provider, cmd.Input.ApiKey!);
        // A different connection is an untested one, and the clearance for client data was given to the old one.
        if (connectionChanged) { provider.LastTestedAt = null; provider.LastTestOk = null; provider.LastTestMessage = null; provider.ConsecutiveFailures = 0; provider.UseForBrand = false; provider.UseForWorkspace = false; }
        log.Add(newKey ? "AI service key replaced" : "AI service changed", null, null, $"{provider.Name}, model {provider.Model}.");
        await providers.SaveChangesAsync(ct);
        return await ViewAsync(ct);
    }

    public async Task<AssistantConsoleView> Handle(DeleteAiProvider cmd, CancellationToken ct)
    {
        await RequireOwnerAsync(ct);
        var provider = await providers.GetForUpdateAsync(cmd.Id, ct);
        providers.Remove(provider);
        log.Add("AI service deleted", null, null, provider.Name);
        await providers.SaveChangesAsync(ct);
        return await ViewAsync(ct);
    }

    public async Task<AssistantConsoleView> Handle(ReorderAiProviders cmd, CancellationToken ct)
    {
        await RequireOwnerAsync(ct);
        var rows = await providers.Tracked().ToListAsync(ct);
        if (rows.Count != cmd.Input.Ids.Count || rows.Any(p => !cmd.Input.Ids.Contains(p.Id))) throw new ConflictException("The list of services has changed. Reload the page and try again.");
        foreach (var p in rows) p.SortOrder = (cmd.Input.Ids.ToList().IndexOf(p.Id) + 1) * 10;
        log.Add("AI service order changed", null, null, string.Join(", ", rows.OrderBy(p => p.SortOrder).Select(p => p.Name)));
        await providers.SaveChangesAsync(ct);
        return await ViewAsync(ct);
    }

    public async Task<AssistantConsoleView> Handle(TestAiProvider cmd, CancellationToken ct)
    {
        await RequireOwnerAsync(ct);
        var provider = await providers.GetForUpdateAsync(cmd.Id, ct);
        var key = protector.TryUnprotect(provider.EncryptedApiKey);
        var result = key is null
            ? new AiConnectionResult(false, "The stored key can no longer be read. Paste it again.")
            : await tester.TestAsync(provider.BaseUrl, provider.Model, key, ct);
        provider.LastTestedAt = clock.UtcNow;
        provider.LastTestOk = result.Ok;
        provider.LastTestMessage = result.Message;
        if (result.Ok) provider.ConsecutiveFailures = 0;
        await providers.SaveChangesAsync(ct);
        return await ViewAsync(ct);
    }

    private static void Apply(AiProvider p, AiProviderInput i)
    {
        p.Name = i.Name.Trim();
        p.BaseUrl = AiEndpointRules.Normalize(i.BaseUrl);
        p.Model = i.Model.Trim();
        p.UseForBrand = i.UseForBrand;
        p.UseForWorkspace = i.UseForWorkspace;
    }

    private void SetKey(AiProvider p, string apiKey)
    {
        var key = apiKey.Trim();
        p.EncryptedApiKey = protector.Protect(key);
        p.ApiKeyHint = key[^4..];
    }
}
