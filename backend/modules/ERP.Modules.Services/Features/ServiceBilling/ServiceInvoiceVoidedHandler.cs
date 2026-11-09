using ERP.Application.Finance;
using ERP.Modules.Services.Repositories;
using MediatR;

namespace ERP.Modules.Services.Features;

/// <summary>Voiding (or deleting a draft of) an invoice raised here frees its time, and the retainer period it billed can be invoiced again.</summary>
public sealed class ServiceInvoiceVoidedHandler(ITimeEntryRecordRepository time, IRetainerBillingPeriodRepository periods) : INotificationHandler<InvoiceVoided>
{
    public async Task Handle(InvoiceVoided n, CancellationToken ct)
    {
        if (n.SourceType is not (ServiceBillingSources.RetainerSourceType or ServiceBillingSources.TimeSourceType)) return;
        var invoiceId = n.InvoiceId;
        foreach (var t in await time.ListOnInvoiceForUpdateAsync(invoiceId, ct)) t.InvoiceId = null;
        foreach (var p in await periods.ListOnInvoiceForUpdateAsync(invoiceId, ct)) periods.Remove(p);
        await time.SaveChangesAsync(ct);
    }
}
