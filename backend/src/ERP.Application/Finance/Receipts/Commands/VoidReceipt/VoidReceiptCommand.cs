using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Voids a posted receipt by reversing its journal and its allocations. Reason is required (3-500 characters, see <see cref="VoidMemo"/>); it goes to the reversal memo and the audit log.</summary>
public sealed record VoidReceiptCommand(Guid Id, string? Reason) : ICommand<ReceiptDto>, IRetryOnConcurrency, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ReceiptsWrite;
}
