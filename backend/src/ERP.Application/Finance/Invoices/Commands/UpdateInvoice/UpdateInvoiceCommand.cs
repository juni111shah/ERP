using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Replaces the header and lines of a draft invoice or credit note.</summary>
public sealed record UpdateInvoiceCommand(Guid Id, InvoiceInput Input) : ICommand<InvoiceDto>, IRetryOnConcurrency, IRequirePermission
{
    public string RequiredPermission => CorePermissions.InvoicesWrite;
}
