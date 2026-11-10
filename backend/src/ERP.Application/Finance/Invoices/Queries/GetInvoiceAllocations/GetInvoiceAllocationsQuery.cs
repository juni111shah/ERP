using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>The customer receipts allocated to one invoice (for the invoice detail view).</summary>
public sealed record GetInvoiceAllocationsQuery(Guid InvoiceId) : IQuery<IReadOnlyList<InvoiceAllocationDto>>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.InvoicesRead;
}
