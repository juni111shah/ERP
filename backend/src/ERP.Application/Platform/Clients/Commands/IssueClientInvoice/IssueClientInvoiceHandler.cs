using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

internal sealed class IssueClientInvoiceHandler(
    ClientSubscriptions clients, IReadRepository<SubscriptionPlan> plans, ISubscriptionInvoiceRepository invoices, IRepository<WorkspaceSubscription> subscriptions,
    SubscriptionInvoicing invoicing, PlatformSettingsStore settingsStore, PlatformLog log, IClock clock) : ICommandHandler<IssueClientInvoiceCommand, InvoiceRow>
{
    public async Task<InvoiceRow> Handle(IssueClientInvoiceCommand command, CancellationToken ct)
    {
        var (org, s) = await clients.LoadAsync(command.OrgId, ct);
        var settings = await settingsStore.GetTrackedAsync(ct);
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var due = command.Input.DueOn ?? today.AddDays(settings.InvoiceDueDays);
        SubscriptionInvoice invoice;
        if (command.Input.Amount is { } amount)
        {
            // A one-off charge.
            invoice = invoicing.Issue(s, settings, amount, command.Input.Description!.Trim(), null, null, today, due);
        }
        else
        {
            // The next period of the plan, once.
            var plan = await plans.GetAsync(s.PlanId, ct);
            if (SubscriptionRules.CyclePrice(s, plan) <= 0) throw new ConflictException("This client has nothing to bill: the price is zero.");
            var start = s.CurrentPeriodEnd is { } end && end >= today ? end.AddDays(1) : today;
            if (await invoices.AnyAsync(x => x.SubscriptionId == s.Id && x.PeriodStart == start && x.Status != SubscriptionInvoiceStatus.Void, ct))
                throw new ConflictException("An invoice for that period already exists.");
            invoice = invoicing.IssueForPeriod(s, plan, settings, start, today, due);
        }
        log.Add("Invoice issued", command.OrgId, org.Name, $"{invoice.Number}: {invoice.Currency} {invoice.Total:N2}");
        await subscriptions.SaveChangesAsync(ct);
        await invoicing.NotifyAsync(invoice, s, settings, ct);
        return InvoiceRow.From(invoice, today);
    }
}
