using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>The lease, property, unit and period behind each of the invoices (at most 200).</summary>
public sealed record GetInvoiceLeaseLinksQuery(IReadOnlyList<Guid> InvoiceIds) : IQuery<IReadOnlyList<InvoiceLeaseLinkDto>>, IRequirePermission
{
    public string RequiredPermission => P.LeasesRead;
}
