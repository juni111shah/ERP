using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Creates a draft invoice or credit note.</summary>
public sealed record CreateInvoiceCommand(InvoiceInput Input) : ICommand<InvoiceDto>, IRetryOnConcurrency, IRequirePermission
{
    public string RequiredPermission => CorePermissions.InvoicesWrite;
}
