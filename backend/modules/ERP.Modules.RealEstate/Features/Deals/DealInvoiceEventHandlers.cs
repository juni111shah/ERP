using ERP.Application.Finance;
using ERP.Modules.RealEstate.Repositories;
using MediatR;

namespace ERP.Modules.RealEstate.Features;

/// <summary>The client paid (or a payment was taken back): move the deal between Invoiced and Collected.</summary>
internal sealed class DealInvoicePaymentChangedHandler(IBrokerageDealRepository deals) : INotificationHandler<InvoicePaymentChanged>
{
    public async Task Handle(InvoicePaymentChanged n, CancellationToken ct)
    {
        if (n.SourceType != DealCommission.SourceType || n.SourceId is not { } id) return;
        var deal = await deals.FindByCommissionInvoiceForUpdateAsync(id, n.InvoiceId, ct);
        if (deal is null) return;
        if (n.IsPaid) deal.MarkCollected(); else deal.ReopenCollected();
        await deals.SaveChangesAsync(ct);
    }
}

/// <summary>The commission invoice was voided: the deal can be invoiced again.</summary>
internal sealed class DealInvoiceVoidedHandler(IBrokerageDealRepository deals) : INotificationHandler<InvoiceVoided>
{
    public async Task Handle(InvoiceVoided n, CancellationToken ct)
    {
        if (n.SourceType != DealCommission.SourceType || n.SourceId is not { } id) return;
        var deal = await deals.FindByCommissionInvoiceForUpdateAsync(id, n.InvoiceId, ct);
        if (deal is null) return;
        deal.ReleaseInvoice();
        await deals.SaveChangesAsync(ct);
    }
}
