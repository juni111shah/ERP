using ERP.Application.Common;
using ERP.Application.Templates;
using MediatR;

namespace ERP.Application.Platform;

public sealed record OnboardingRequest(
    string WorkspaceName, string SignInKey, string Industry, string CountryCode, string BaseCurrency, string? TimeZone,
    string? LegalEntityName, string AdminEmail, string AdminName, string AdminPassword);

public sealed record OnboardingResult(Guid OrgId, Guid LegalEntityId, Guid AdminUserId, IReadOnlyList<string> EnabledModules, IReadOnlyList<string> UnavailableModules);

public sealed record IndustryView(string Key, string Label, string Description, string Availability, IReadOnlyList<string> SuggestedModules, IReadOnlyList<string> InstalledModules, int SortOrder);

/// <summary>Creates a workspace from an industry template and country pack (org, entity, seed data, modules, admin). Implemented in Infrastructure.</summary>
public interface IOnboardingService
{
    bool SelfSignupEnabled { get; }
    IReadOnlyList<IndustryView> IndustryViews();
    Task<OnboardingResult> CreateWorkspaceAsync(OnboardingRequest request, CancellationToken ct = default);
}

public sealed record ListIndustries : IQuery<IReadOnlyList<IndustryView>>;
public sealed record CreateWorkspace(OnboardingRequest Request) : ICommand<OnboardingResult>;

internal sealed class PlatformHandlers(IOnboardingService onboarding) :
    IRequestHandler<ListIndustries, IReadOnlyList<IndustryView>>,
    IRequestHandler<CreateWorkspace, OnboardingResult>
{
    public Task<IReadOnlyList<IndustryView>> Handle(ListIndustries q, CancellationToken ct) => Task.FromResult(onboarding.IndustryViews());

    public Task<OnboardingResult> Handle(CreateWorkspace cmd, CancellationToken ct)
        => onboarding.SelfSignupEnabled ? onboarding.CreateWorkspaceAsync(cmd.Request, ct) : throw new ForbiddenException("Self sign-up is disabled.");
}
