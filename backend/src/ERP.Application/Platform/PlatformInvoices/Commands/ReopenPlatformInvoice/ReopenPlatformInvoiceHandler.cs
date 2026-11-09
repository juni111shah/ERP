using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

internal sealed class ReopenPlatformInvoiceHandler(
    ISubscriptionInvoiceRepository invoices, IWorkspaceSubscriptionRepository subscriptions, IOrganizationRepository organizations, ISubscriptionAccess access,
    PlatformInvoiceDocuments documents, PlatformLog log) : ICommandHandler<ReopenPlatformInvoiceCommand, InvoiceDocument>
{
    public async Task<InvoiceDocument> Handle(ReopenPlatformInvoiceCommand command, CancellationToken ct)
    {
        var invoice = await invoices.GetForUpdateAsync(command.Id, ct);
        if (invoice.Status != SubscriptionInvoiceStatus.Paid) throw new ConflictException("Only a paid invoice can be reopened.");
        invoice.Status = SubscriptionInvoiceStatus.Open;
        invoice.PaidOn = null; invoice.PaymentMethod = null; invoice.PaymentReference = null;
        var org = await organizations.GetAsync(invoice.OrgId, ct);
        log.Add("Payment taken back", invoice.OrgId, org.Name, invoice.Number);
        await subscriptions.SaveChangesAsync(ct);
        access.Invalidate(invoice.OrgId);
        return await documents.DocumentAsync(invoice, ct);
    }
}
