using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Platform;

/// <summary>One of the caller's workspace's invoices, ready to print.</summary>
public sealed record GetMyInvoiceQuery(Guid Id) : IQuery<InvoiceDocument>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.BillingRead;
}
