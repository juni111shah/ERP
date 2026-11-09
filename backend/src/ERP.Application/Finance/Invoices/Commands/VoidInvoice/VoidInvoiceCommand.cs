using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Voids an issued invoice by reversing its journal. Reason is required (3-500 characters, see <see cref="VoidMemo"/>); it goes to the reversal memo and the audit log.</summary>
public sealed record VoidInvoiceCommand(Guid Id, string? Reason) : ICommand<InvoiceDto>, IRetryOnConcurrency, IRequirePermission
{
    public string RequiredPermission => CorePermissions.InvoicesVoid;
}
