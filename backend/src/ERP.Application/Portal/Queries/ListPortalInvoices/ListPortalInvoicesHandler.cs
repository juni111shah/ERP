using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;

namespace ERP.Application.Portal;

internal sealed class ListPortalInvoicesHandler(IInvoiceRepository invoices, ICurrentUser user) : IQueryHandler<ListPortalInvoicesQuery, IReadOnlyList<PortalInvoiceDto>>
{
    public async Task<IReadOnlyList<PortalInvoiceDto>> Handle(ListPortalInvoicesQuery query, CancellationToken ct)
    {
        var me = PortalCaller.Customer(user);
        return await invoices.ListForPortalAsync(me, ct);
    }
}
