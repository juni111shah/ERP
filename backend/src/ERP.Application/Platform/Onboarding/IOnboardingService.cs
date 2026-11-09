namespace ERP.Application.Platform;

/// <summary>Creates a workspace from an industry template and country pack (org, entity, seed data, modules, admin). Implemented in Infrastructure.</summary>
public interface IOnboardingService
{
    bool SelfSignupEnabled { get; }
    IReadOnlyList<IndustryView> IndustryViews();
    Task<OnboardingResult> CreateWorkspaceAsync(OnboardingRequest request, CancellationToken ct = default);
    Task<OnboardingResult> CreateWorkspaceAsync(OnboardingRequest request, OnboardingOptions? options, CancellationToken ct = default);
}
