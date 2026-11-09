using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

internal sealed class RecordInvoicePaymentHandler(
    ISubscriptionInvoiceRepository invoices, IWorkspaceSubscriptionRepository subscriptions, IOrganizationRepository organizations, ISubscriptionAccess access,
    PlatformInvoiceDocuments documents, PlatformLog log, IPlatformMailer mailer, IClock clock) : ICommandHandler<RecordInvoicePaymentCommand, InvoiceDocument>
{
    public async Task<InvoiceDocument> Handle(RecordInvoicePaymentCommand command, CancellationToken ct)
    {
        // 1. Only an open invoice, paid today or earlier.
        var invoice = await invoices.GetForUpdateAsync(command.Id, ct);
        if (invoice.Status != SubscriptionInvoiceStatus.Open) throw new ConflictException("Only an open invoice can take a payment.");
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var paidOn = command.Input.PaidOn ?? today;
        if (paidOn > today) throw new FieldValidationException("paidOn", "The payment date cannot be in the future.");

        // 2. Mark it paid and bring the subscription up to date.
        invoice.Status = SubscriptionInvoiceStatus.Paid;
        invoice.PaidOn = paidOn;
        invoice.PaymentMethod = command.Input.Method?.Trim();
        invoice.PaymentReference = command.Input.Reference?.Trim();
        var s = await subscriptions.FindForClientForUpdateAsync(invoice.OrgId, ct);
        var org = await organizations.GetAsync(invoice.OrgId, ct);
        if (s is not null) SubscriptionRules.ApplyPayment(s, invoice, await invoices.HasOverdueAsync(invoice.OrgId, invoice.Id, DateOnly.FromDateTime(clock.UtcNow), ct), today);
        log.Add("Payment recorded", invoice.OrgId, org.Name, $"{invoice.Number}: {invoice.Currency} {invoice.Total:N2}{(invoice.PaymentMethod is { Length: > 0 } m ? $" by {m}" : "")}");
        await subscriptions.SaveChangesAsync(ct);
        access.Invalidate(invoice.OrgId);

        // 3. Thank the client.
        await mailer.SendAsync(s?.BillingEmail, $"Payment received for {invoice.Number}", $"Thank you. We received {invoice.Currency} {invoice.Total:N2} for invoice {invoice.Number}.", ct);
        return await documents.DocumentAsync(invoice, ct);
    }
}
