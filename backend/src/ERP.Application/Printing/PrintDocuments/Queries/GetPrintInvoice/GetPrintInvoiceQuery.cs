using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Printing;

/// <summary>Get print data for an invoice or credit note.</summary>
public sealed record GetPrintInvoiceQuery(Guid Id) : IQuery<PrintInvoiceDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.InvoicesRead;
}
