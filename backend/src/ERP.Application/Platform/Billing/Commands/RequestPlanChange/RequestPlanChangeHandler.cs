using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Organizations;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

internal sealed class RequestPlanChangeHandler(
    IWorkspaceSubscriptionRepository subscriptions, ISubscriptionPlanRepository plans, IReadRepository<Organization> organizations, BillingOverviews overviews,
    PlatformSettingsStore settingsStore, PlatformLog log, IPlatformMailer mailer, ICurrentUser user, IClock clock) : ICommandHandler<RequestPlanChangeCommand, BillingOverview>
{
    public async Task<BillingOverview> Handle(RequestPlanChangeCommand command, CancellationToken ct)
    {
        // 1. The workspace's subscription and a public plan to move to.
        var orgId = user.OrgId ?? throw new ForbiddenException();
        var s = await subscriptions.FindForClientForUpdateAsync(orgId, ct) ?? throw new ConflictException("This workspace has no subscription to change.");
        var plan = await plans.FindPublicAsync(command.Input.PlanId, ct) ?? throw new FieldValidationException("planId", "Choose a plan from the list.");

        // 2. Record the request.
        s.RequestedPlanId = plan.Id;
        s.RequestedCycle = command.Input.Cycle;
        s.RequestNote = command.Input.Note?.Trim();
        s.RequestedAt = clock.UtcNow;
        var client = await organizations.GetAsync(orgId, ct);
        log.Add("Plan change requested", orgId, client.Name, $"{plan.Name}, {(command.Input.Cycle == BillingCycle.Annual ? "annual" : "monthly")}. {s.RequestNote}");
        await subscriptions.SaveChangesAsync(ct);

        // 3. Tell the founder.
        var settings = await settingsStore.GetAsync(ct);
        await mailer.SendAsync(settings.SupportEmail, $"Plan change request: {client.Name}", $"{client.Name} asked for the {plan.Name} plan.\n\n{s.RequestNote}\n\n{mailer.Link($"/platform/clients/{orgId}")}", ct);
        return await overviews.GetAsync(orgId, ct);
    }
}
