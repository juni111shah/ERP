using ERP.Application.Common;
using ERP.Application.Templates;
using MediatR;

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

/// <summary>Creates a workspace from an industry template and country pack (org, entity, seed data, modules, admin). Implemented in Infrastructure.</summary>
public interface IOnboardingService
{
    bool SelfSignupEnabled { get; }
    IReadOnlyList<IndustryView> IndustryViews();
    Task<OnboardingResult> CreateWorkspaceAsync(OnboardingRequest request, CancellationToken ct = default);
    Task<OnboardingResult> CreateWorkspaceAsync(OnboardingRequest request, OnboardingOptions? options, CancellationToken ct = default);
}

public sealed record ListIndustries : IQuery<IReadOnlyList<IndustryView>>;
public sealed record CreateWorkspace(OnboardingRequest Request) : ICommand<OnboardingResult>;

internal sealed class PlatformHandlers(IOnboardingService onboarding, PlatformSettingsStore settingsStore, ERP.Application.Abstractions.IReadRepository<ERP.Domain.Platform.SubscriptionPlan> plans, ERP.Application.Abstractions.IClock clock) :
    IRequestHandler<ListIndustries, IReadOnlyList<IndustryView>>,
    IRequestHandler<CreateWorkspace, OnboardingResult>
{
    public Task<IReadOnlyList<IndustryView>> Handle(ListIndustries q, CancellationToken ct) => Task.FromResult(onboarding.IndustryViews());

    /// <summary>The older sign-up route, on only where a deployment switches it on. It starts on the default plan like any registration, so it is no way round billing.</summary>
    public async Task<OnboardingResult> Handle(CreateWorkspace cmd, CancellationToken ct)
    {
        if (!onboarding.SelfSignupEnabled) throw new ForbiddenException("Self sign-up is disabled.");
        var settings = await settingsStore.GetAsync(ct);
        var plan = settings.DefaultPlanId is { } id ? await plans.FindAsync(id, ct) : null;
        if (plan is null) return await onboarding.CreateWorkspaceAsync(cmd.Request, ct);
        var subscription = SubscriptionRules.Start(plan, ERP.Domain.Platform.BillingCycle.Monthly, DateOnly.FromDateTime(clock.UtcNow), ClientStartMode.Trial);
        subscription.BillingEmail = cmd.Request.AdminEmail?.Trim();
        return await onboarding.CreateWorkspaceAsync(cmd.Request, new OnboardingOptions(Subscription: subscription, AllowedModules: plan.ModuleList()), ct);
    }
}
