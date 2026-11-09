using ERP.Application.Assistant;

namespace ERP.Application.Platform;

public sealed record AiProviderView(
    Guid Id, string Name, string BaseUrl, string Model, string KeyHint, bool KeyNeedsReentry, bool UseForBrand, bool UseForWorkspace,
    DateTime? LastTestedAt, bool? LastTestOk, string? LastTestMessage);

public sealed record AssistantConsoleView(
    bool BrandEnabled, bool WorkspaceEnabled, bool CanManage, IReadOnlyList<AiProviderView> Providers,
    int BrandDailyTokens, int WorkspaceDailyTokens, IReadOnlyList<AssistantUsageRow> UsageToday);

/// <summary>ApiKey is write-only. Leave it empty when editing to keep the stored key.</summary>
public sealed record AiProviderInput(string Name, string BaseUrl, string Model, string? ApiKey, bool UseForBrand, bool UseForWorkspace);
public sealed record AssistantVisibilityInput(bool BrandEnabled, bool WorkspaceEnabled);
public sealed record AiProviderOrderInput(IReadOnlyList<Guid> Ids);
public sealed record AssistantLimitsInput(int BrandDailyTokens, int WorkspaceDailyTokens);
