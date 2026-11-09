namespace ERP.Application.Platform;

public sealed record OnboardingRequest(
    string WorkspaceName, string SignInKey, string Industry, string CountryCode, string BaseCurrency, string? TimeZone,
    string? LegalEntityName, string AdminEmail, string AdminName, string AdminPassword);

public sealed record OnboardingResult(Guid OrgId, Guid LegalEntityId, Guid AdminUserId, IReadOnlyList<string> EnabledModules, IReadOnlyList<string> UnavailableModules, bool AdminAccountCreated = false);

/// <summary>
/// What trusted callers add to a new workspace. Kept apart from <see cref="OnboardingRequest"/>, which is bound from a public
/// request body, so none of this can ever be sent from outside.
/// </summary>
/// <param name="AdminPasswordHash">A registration's stored hash, used in place of a password.</param>
/// <param name="TrustExistingAccount">The account's password was already confirmed, or a platform admin is adding this workspace to it.</param>
/// <param name="AllowedModules">The plan's modules; empty means every module the industry suggests.</param>
public sealed record OnboardingOptions(
    string? AdminPasswordHash = null, bool TrustExistingAccount = false, bool AdminMustChangePassword = false, bool IsInternal = false,
    ERP.Domain.Platform.WorkspaceSubscription? Subscription = null, IReadOnlyList<string>? AllowedModules = null, bool IsDemo = false);

public sealed record IndustryView(string Key, string Label, string Description, string Availability, IReadOnlyList<string> SuggestedModules, IReadOnlyList<string> InstalledModules, int SortOrder, string? Group = null);
