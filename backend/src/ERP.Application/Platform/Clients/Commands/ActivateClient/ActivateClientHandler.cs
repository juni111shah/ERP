using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

internal sealed class ActivateClientHandler(
    ClientSubscriptions clients, IReadRepository<SubscriptionPlan> plans, SubscriptionInvoicing invoicing, PlatformSettingsStore settingsStore, PlatformLog log, IClock clock)
    : ICommandHandler<ActivateClientCommand, ClientDetail>
{
    public async Task<ClientDetail> Handle(ActivateClientCommand command, CancellationToken ct)
    {
        // 1. Not while a paid period is still running.
        var (org, s) = await clients.LoadAsync(command.OrgId, ct);
        var plan = await plans.GetAsync(s.PlanId, ct);
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var running = s.Status == SubscriptionStatus.Active && s.CurrentPeriodEnd is { } end && end >= today;
        if (running) throw new ConflictException("This client is already active.");

        // 2. Start the period and clear every lapse, suspension and cancellation.
        s.Status = SubscriptionStatus.Active;
        s.CurrentPeriodStart = today;
        s.CurrentPeriodEnd = SubscriptionRules.PeriodEnd(today, s.Cycle);
        s.LapsedOn = null; s.SuspendedAt = null; s.SuspendedReason = null; s.SuspendedByFounder = false; s.CancelledAt = null; s.CancelReason = null; s.LastReminderKey = null;

        // 3. Invoice the period when asked and there is something to bill.
        SubscriptionInvoice? invoice = null;
        var settings = await settingsStore.GetTrackedAsync(ct);
        if (command.IssueInvoice && SubscriptionRules.CyclePrice(s, plan) > 0)
            invoice = invoicing.IssueForPeriod(s, plan, settings, today, today, today.AddDays(settings.InvoiceDueDays));
        log.Add("Activated", command.OrgId, org.Name, invoice is null ? $"Paid period to {s.CurrentPeriodEnd:d MMM yyyy}, no invoice." : $"Paid period to {s.CurrentPeriodEnd:d MMM yyyy}, invoice {invoice.Number}.");
        var detail = await clients.SavedAsync(command.OrgId, ct);
        if (invoice is not null) await invoicing.NotifyAsync(invoice, s, settings, ct);
        return detail;
    }
}
