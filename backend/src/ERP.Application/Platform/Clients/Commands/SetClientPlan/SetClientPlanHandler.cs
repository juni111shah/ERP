using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

internal sealed class SetClientPlanHandler(
    IOrganizationRepository organizations, IReadRepository<SubscriptionPlan> plans, IWorkspaceSubscriptionRepository subscriptions, SubscriptionChanges changes, IPlatformDirectory directory,
    ClientProvisioner provisioner, ClientReader clients, PlatformLog log, IClock clock) : ICommandHandler<SetClientPlanCommand, ClientDetail>
{
    private DateOnly Today => DateOnly.FromDateTime(clock.UtcNow);

    public async Task<ClientDetail> Handle(SetClientPlanCommand command, CancellationToken ct)
    {
        // 1. A real client, an existing plan and a sensible price.
        var i = command.Input;
        var org = await organizations.GetAsync(command.OrgId, ct);
        if (org.IsInternal) throw new ConflictException("The platform's own workspace has no subscription.");
        var plan = await plans.FindAsync(i.PlanId, ct);
        if (plan is null) throw new FieldValidationException("planId", "Choose a plan.");
        if (i.CustomPrice is < 0) throw new FieldValidationException("customPrice", "The price cannot be negative.");

        // 2. A first plan starts a subscription; a change must not move to an archived plan.
        var s = await subscriptions.FindForClientForUpdateAsync(command.OrgId, ct);
        var isNew = s is null;
        if (s is null)
        {
            if (!plan.IsActive) throw new FieldValidationException("planId", "Choose an active plan.");
            s = SubscriptionRules.Start(plan, i.Cycle, Today, i.IsComplimentary ? ClientStartMode.Complimentary : i.StartMode ?? ClientStartMode.Trial, i.TrialDays);
            s.OrgId = command.OrgId;
            subscriptions.Add(s);
        }
        else if (!plan.IsActive && plan.Id != s.PlanId) throw new FieldValidationException("planId", "That plan is archived. Choose an active plan.");

        // 3. Apply the plan, cycle and price; a matching plan request is fulfilled.
        var wasPlan = await plans.FindAsync(s.PlanId, ct);
        s.PlanId = plan.Id;
        s.Cycle = i.Cycle;
        s.CustomPrice = i.CustomPrice;
        if (i.IsComplimentary && !s.IsComplimentary) { s.Status = s.Status is SubscriptionStatus.Cancelled or SubscriptionStatus.Suspended ? s.Status : SubscriptionStatus.Active; s.LapsedOn = null; }
        s.IsComplimentary = i.IsComplimentary;
        if (s.RequestedPlanId == plan.Id) { s.RequestedPlanId = null; s.RequestedCycle = null; s.RequestNote = null; s.RequestedAt = null; }
        var modules = plan.ModuleList();
        log.Add("Plan changed", command.OrgId, org.Name, $"{wasPlan?.Name ?? "None"} to {plan.Name}, {(i.Cycle == BillingCycle.Annual ? "annual" : "monthly")}{(i.CustomPrice is { } price ? $", custom price {price:N2}" : "")}{(i.IsComplimentary ? ", free of charge" : "")}.");
        var before = await changes.BeforeAsync(command.OrgId, ct);
        await subscriptions.SaveChangesAsync(ct);

        // 4. Modules outside the plan are switched off. The client's people see the new plan at once; when modules went, their old tokens end
        // too, so a screen that missed the push still refreshes on its next call.
        var removed = modules.Count > 0 ? await directory.DisableModulesOutsideAsync(command.OrgId, modules, ct) : 0;
        await changes.AfterAsync(command.OrgId, before, ct, modulesRemoved: removed > 0);
        // A first plan that starts as paid bills its first period, the same as a client created with that plan.
        if (isNew) await provisioner.IssueFirstInvoiceAsync(s, plan, Today, ct);
        return await clients.DetailAsync(command.OrgId, ct);
    }
}
