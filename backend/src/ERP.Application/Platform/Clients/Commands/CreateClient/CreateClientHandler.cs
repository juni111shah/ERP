using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

internal sealed class CreateClientHandler(
    IReadRepository<SubscriptionPlan> plans, IRepository<WorkspaceSubscription> subscriptions, IOnboardingService onboarding, IPlatformDirectory directory, ClientProvisioner provisioner,
    PlatformLog log, IClock clock) : ICommandHandler<CreateClientCommand, CreatedClient>
{
    public async Task<CreatedClient> Handle(CreateClientCommand command, CancellationToken ct)
    {
        // 1. The plan must be active; the subscription starts as asked.
        var i = command.Input;
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var plan = await plans.FindAsync(i.PlanId, ct);
        if (plan is null || !plan.IsActive) throw new FieldValidationException("planId", "Choose an active plan.");
        var subscription = SubscriptionRules.Start(plan, i.Cycle, today, i.StartMode, i.TrialDays);
        subscription.CustomPrice = i.CustomPrice;
        subscription.BillingEmail = i.AdminEmail.Trim();
        subscription.ContactPhone = i.Phone?.Trim();
        subscription.Notes = i.Notes?.Trim();

        // 2. Create the workspace and its first administrator.
        var password = directory.NewTemporaryPassword();
        var result = await onboarding.CreateWorkspaceAsync(
            new OnboardingRequest(i.WorkspaceName, i.SignInKey, i.Industry, i.CountryCode, i.BaseCurrency, i.TimeZone, i.WorkspaceName, i.AdminEmail, i.AdminName, password),
            new OnboardingOptions(null, TrustExistingAccount: true, AdminMustChangePassword: true, Subscription: subscription, AllowedModules: plan.ModuleList()), ct);
        log.Add("Client created", result.OrgId, i.WorkspaceName.Trim(), $"By hand. {plan.Name} plan, first administrator {i.AdminEmail.Trim()}.");
        await subscriptions.SaveChangesAsync(ct);

        // 3. A paid start bills its first period.
        await provisioner.IssueFirstInvoiceAsync(subscription, plan, DateOnly.FromDateTime(clock.UtcNow), ct);
        return new(result.OrgId, i.SignInKey.Trim().ToLowerInvariant(), i.AdminEmail.Trim(), result.AdminAccountCreated ? password : null);
    }
}
