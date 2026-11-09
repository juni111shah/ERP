using ERP.Application.Abstractions;
using ERP.Domain.Identity;
using ERP.Domain.Organizations;

namespace ERP.Application.Platform;

/// <summary>Builds a client's own billing page. Shared by the billing query and the plan-change commands, which answer with the updated page.</summary>
internal sealed class BillingOverviews(
    IWorkspaceSubscriptionRepository subscriptions, ISubscriptionPlanRepository plans, ISubscriptionInvoiceRepository invoices, IReadRepository<OrganizationMember> members,
    IReadRepository<LegalEntity> legalEntities, ISubscriptionAccess access, PlatformSettingsStore settingsStore, IClock clock)
{
    public async Task<BillingOverview> GetAsync(Guid orgId, CancellationToken ct)
    {
        var settings = await settingsStore.GetAsync(ct);
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var s = await subscriptions.FindForClientAsync(orgId, ct);
        var plan = s is null ? null : await plans.FindAsync(s.PlanId, ct);
        var available = await plans.ListPublicAsync(ct);
        var rows = await invoices.ListForClientAsync(orgId, ct);
        RequestedChange? requested = null;
        if (s?.RequestedPlanId is { } requestedId && await plans.FindAsync(requestedId, ct) is { } target)
            requested = new(target.Id, target.Name, s.RequestedCycle ?? s.Cycle, s.RequestNote, s.RequestedAt ?? clock.UtcNow);
        return new(
            await access.GetAsync(orgId, ct), s?.PlanId, s?.Cycle, s is null || plan is null ? null : SubscriptionRules.CyclePrice(s, plan), settings.Currency,
            await members.CountAsync(m => m.IsActive, ct), plan?.MaxUsers, await legalEntities.CountAsync(null, ct), plan?.MaxLegalEntities, requested,
            available.Select(p => new PublicPlan(p.Id, p.Code, p.Name, p.Description, p.MonthlyPrice, p.AnnualPrice, settings.Currency, p.TrialDays, p.MaxUsers, p.MaxLegalEntities, p.ModuleList(), p.FeatureList(), p.IsRecommended)).ToList(),
            rows.Select(i => InvoiceRow.From(i, today)).ToList(), settings.PaymentInstructions, settings.SupportEmail, settings.TermsUrl);
    }
}
