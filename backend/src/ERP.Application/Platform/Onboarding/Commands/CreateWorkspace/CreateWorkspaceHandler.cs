using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

internal sealed class CreateWorkspaceHandler(IOnboardingService onboarding, PlatformSettingsStore settingsStore, IReadRepository<SubscriptionPlan> plans, IClock clock)
    : ICommandHandler<CreateWorkspaceCommand, OnboardingResult>
{
    public async Task<OnboardingResult> Handle(CreateWorkspaceCommand command, CancellationToken ct)
    {
        // 1. Only where the deployment allows it.
        if (!onboarding.SelfSignupEnabled) throw new ForbiddenException("Self sign-up is disabled.");

        // 2. Without a default plan the workspace starts with no subscription.
        var settings = await settingsStore.GetAsync(ct);
        var plan = settings.DefaultPlanId is { } id ? await plans.FindAsync(id, ct) : null;
        if (plan is null) return await onboarding.CreateWorkspaceAsync(command.Request, ct);

        // 3. Otherwise it starts a trial of the default plan.
        var subscription = SubscriptionRules.Start(plan, BillingCycle.Monthly, DateOnly.FromDateTime(clock.UtcNow), ClientStartMode.Trial);
        subscription.BillingEmail = command.Request.AdminEmail?.Trim();
        return await onboarding.CreateWorkspaceAsync(command.Request, new OnboardingOptions(Subscription: subscription, AllowedModules: plan.ModuleList()), ct);
    }
}
