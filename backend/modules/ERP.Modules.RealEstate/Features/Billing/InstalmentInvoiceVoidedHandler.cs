using ERP.Application.Finance;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Repositories;
using MediatR;

namespace ERP.Modules.RealEstate.Features;

/// <summary>When an invoice made from an instalment is voided, the instalment becomes billable again.</summary>
internal sealed class InstalmentInvoiceVoidedHandler(IRentInstalmentRepository instalments) : INotificationHandler<InvoiceVoided>
{
    public async Task Handle(InvoiceVoided n, CancellationToken ct)
    {
        if (n.SourceType != RentInvoicer.SourceType || n.SourceId is not { } id) return;
        var i = await instalments.FindInvoicedByForUpdateAsync(id, n.InvoiceId, ct);
        if (i is null) return;
        i.InvoiceId = null; i.Status = InstalmentStatus.Scheduled; i.CreditedToOwner = false;
        await instalments.SaveChangesAsync(ct);
    }
}
