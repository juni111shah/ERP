using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

/// <summary>Builds the console's client rows and the full client page. Shared by the dashboard, the client list and every client command.</summary>
internal sealed class ClientReader(
    IWorkspaceSubscriptionRepository subscriptions, ISubscriptionPlanRepository plans, ISubscriptionInvoiceRepository invoices, IOrganizationRepository organizations,
    IPlatformActivityRepository activity, IPlatformDirectory directory, PlatformSettingsStore settingsStore, IClock clock)
{
    private DateOnly Today => DateOnly.FromDateTime(clock.UtcNow);

    /// <summary>Every client (or only <paramref name="onlyOrg"/>) with its plan, state, usage and open balance. The platform's own workspace is left out.</summary>
    public async Task<List<ClientRow>> RowsAsync(Guid? onlyOrg, CancellationToken ct)
    {
        var settings = await settingsStore.GetAsync(ct);
        var orgs = await organizations.ListClientsAsync(onlyOrg, ct);
        var subs = await subscriptions.ByOrgAsync(ct);
        var planById = await plans.ByIdAsync(ct);
        var usage = await directory.UsageAsync(onlyOrg is { } id ? [id] : null, ct);
        var balances = await invoices.OpenBalanceByClientAsync(ct);
        return orgs.Select(o =>
        {
            var s = subs.GetValueOrDefault(o.Id);
            var plan = s is null ? null : planById.GetValueOrDefault(s.PlanId);
            var u = usage.GetValueOrDefault(o.Id);
            return new ClientRow(
                o.Id, o.Name, o.Code, o.IndustryCode, o.CountryCode, o.CreatedAt, s?.PlanId, plan?.Name, s?.Status, s is null ? SubscriptionAccessLevel.Full : SubscriptionRules.AccessOf(s, settings, Today),
                s?.Cycle, s is null || plan is null ? null : SubscriptionRules.CyclePrice(s, plan), s is null || plan is null ? 0 : SubscriptionRules.MonthlyValue(s, plan), s?.IsComplimentary ?? false,
                s?.TrialEndsOn, s?.CurrentPeriodEnd, u?.ActiveUsers ?? 0, plan?.MaxUsers, u?.LastSignInAt, balances.GetValueOrDefault(o.Id), s?.RequestedPlanId is not null);
        }).ToList();
    }

    /// <summary>One client's page: its row, subscription, members, invoices and recent activity. Throws NotFoundException.</summary>
    public async Task<ClientDetail> DetailAsync(Guid orgId, CancellationToken ct)
    {
        var row = (await RowsAsync(orgId, ct)).FirstOrDefault() ?? throw new NotFoundException();
        var org = await organizations.GetAsync(orgId, ct);
        var s = await subscriptions.FindForClientAsync(orgId, ct);
        var plan = s is null ? null : await plans.FindAsync(s.PlanId, ct);
        var usage = (await directory.UsageAsync([orgId], ct)).GetValueOrDefault(orgId);
        RequestedChange? requested = null;
        if (s?.RequestedPlanId is { } requestedId && await plans.FindAsync(requestedId, ct) is { } target)
            requested = new(target.Id, target.Name, s.RequestedCycle ?? s.Cycle, s.RequestNote, s.RequestedAt ?? clock.UtcNow);
        var rows = await invoices.ListForClientAsync(orgId, ct);
        var log = await activity.ListLatestForClientAsync(orgId, 30, ct);
        return new(row, org.BaseCurrency, org.TimeZone, s?.BillingEmail, s?.ContactPhone, s?.Notes, s?.CustomPrice, s?.StartedOn, s?.CurrentPeriodStart, s?.LapsedOn, s?.SuspendedReason, s?.SuspendedByFounder ?? false, s?.CancelledAt, s?.CancelReason, requested,
            usage?.LegalEntities ?? 0, plan?.MaxLegalEntities, usage?.EnabledModules ?? [], plan?.ModuleList(), await directory.MembersAsync(orgId, ct), rows.Select(i => InvoiceRow.From(i, Today)).ToList(), log.Select(ActivityRow.From).ToList());
    }
}
