using ERP.Application.Auth;

namespace ERP.Modularity.Features;

public sealed record MeUser(Guid Id, string Email, string FullName, bool MustChangePassword);
public sealed record MeWorkspace(Guid Id, string Code, string Name, string IndustryCode, string CountryCode, string BaseCurrency, string TimeZone, string DefaultLocale, bool IsDemo = false);
public sealed record MeLegalEntity(Guid Id, string Code, string Name, string BaseCurrency);
public sealed record MeModule(string Key, string UiRoute);

/// <summary>Everything the UI needs after sign-in: identity, workspace, permissions, legal entities and enabled modules.</summary>
public sealed record MeDto(
    MeUser User, MeWorkspace Workspace, IReadOnlyList<WorkspaceOption> Workspaces, IReadOnlyList<string> Permissions,
    IReadOnlyList<MeLegalEntity> LegalEntities, bool AllLegalEntities, IReadOnlyList<MeModule> EnabledModules,
    bool PlatformAdmin, ERP.Application.Platform.SubscriptionSummary Subscription, bool IsInternalWorkspace);
