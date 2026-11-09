using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>One invoice or credit note with its lines.</summary>
public sealed record GetInvoiceByIdQuery(Guid Id) : IQuery<InvoiceDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.InvoicesRead;
}
