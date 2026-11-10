using ERP.Application.Abstractions;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

/// <summary>Turns an accepted registration into a working client: workspace, first administrator and subscription, all in one transaction.</summary>
internal sealed class ClientProvisioner(IOnboardingService onboarding, SubscriptionInvoicing invoicing, PlatformSettingsStore settingsStore, IRepository<SubscriptionPlan> plans, IPlatformMailer mailer, IClock clock)
{
    public async Task<OnboardingResult> ProvisionAsync(SignupRequest r, CancellationToken ct)
    {
        var plan = await plans.GetAsync(r.PlanId, ct);
        var request = new OnboardingRequest(r.BusinessName, r.SignInKey, r.Industry, r.CountryCode, r.BaseCurrency, r.TimeZone, r.BusinessName, r.Email, r.ContactName, "");
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var subscription = SubscriptionRules.Start(plan, r.Cycle, today, ClientStartMode.Trial);
        subscription.BillingEmail = r.Email;
        subscription.ContactPhone = r.Phone;
        var result = await onboarding.CreateWorkspaceAsync(request, new OnboardingOptions(r.PasswordHash, TrustExistingAccount: r.PasswordHash is null, Subscription: subscription, AllowedModules: plan.ModuleList()), ct);
        await IssueFirstInvoiceAsync(subscription, plan, today, ct);

        await mailer.SendAsync(r.Email, "Your workspace is ready",
            $"{r.BusinessName} is ready. Sign in with this email and the password you chose:\n{mailer.Link("/auth/login")}\n\nWorkspace code: {r.SignInKey}", ct);
        return result;
    }

    /// <summary>A paid plan with no trial starts running straight away, so its first invoice goes out with it.</summary>
    public async Task IssueFirstInvoiceAsync(WorkspaceSubscription subscription, SubscriptionPlan plan, DateOnly today, CancellationToken ct)
    {
        if (subscription.Status != SubscriptionStatus.Active || subscription.IsComplimentary || SubscriptionRules.CyclePrice(subscription, plan) <= 0) return;
        var settings = await settingsStore.GetTrackedAsync(ct);
        var invoice = invoicing.IssueForPeriod(subscription, plan, settings, today, today, today.AddDays(settings.InvoiceDueDays));
        await plans.SaveChangesAsync(ct);
        await invoicing.NotifyAsync(invoice, subscription, settings, ct);
    }
}
