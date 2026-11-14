using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

internal sealed class VoidPlatformInvoiceHandler(
    ISubscriptionInvoiceRepository invoices, IWorkspaceSubscriptionRepository subscriptions, IOrganizationRepository organizations, SubscriptionChanges changes,
    PlatformInvoiceDocuments documents, PlatformLog log, IClock clock) : ICommandHandler<VoidPlatformInvoiceCommand, InvoiceDocument>
{
    public async Task<InvoiceDocument> Handle(VoidPlatformInvoiceCommand command, CancellationToken ct)
    {
        var invoice = await invoices.GetForUpdateAsync(command.Id, ct);
        if (invoice.Status != SubscriptionInvoiceStatus.Open) throw new ConflictException("Only an open invoice can be voided. Reopen a paid invoice first.");
        invoice.Status = SubscriptionInvoiceStatus.Void;
        invoice.VoidReason = command.Reason?.Trim();
        var s = await subscriptions.FindForClientForUpdateAsync(invoice.OrgId, ct);
        var org = await organizations.GetAsync(invoice.OrgId, ct);
        if (s is { Status: SubscriptionStatus.PastDue } && !await invoices.HasOverdueAsync(invoice.OrgId, invoice.Id, DateOnly.FromDateTime(clock.UtcNow), ct)) { s.Status = SubscriptionStatus.Active; s.LapsedOn = null; s.LastReminderKey = null; }
        log.Add("Invoice voided", invoice.OrgId, org.Name, $"{invoice.Number}. {invoice.VoidReason}");
        var before = await changes.BeforeAsync(invoice.OrgId, ct);
        await subscriptions.SaveChangesAsync(ct);
        await changes.AfterAsync(invoice.OrgId, before, ct);
        return await documents.DocumentAsync(invoice, ct);
    }
}
