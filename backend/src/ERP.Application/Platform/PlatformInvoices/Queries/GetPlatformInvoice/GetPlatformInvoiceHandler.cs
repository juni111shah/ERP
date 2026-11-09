using ERP.Application.Common;

namespace ERP.Application.Platform;

internal sealed class GetPlatformInvoiceHandler(ISubscriptionInvoiceRepository invoices, PlatformInvoiceDocuments documents) : IQueryHandler<GetPlatformInvoiceQuery, InvoiceDocument>
{
    public async Task<InvoiceDocument> Handle(GetPlatformInvoiceQuery query, CancellationToken ct) => await documents.DocumentAsync(await invoices.GetAsync(query.Id, ct), ct);
}
