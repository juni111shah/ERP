using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Voids a posted, unpaid bill by reversing its journal. Reason is required (3-500 characters, see <see cref="VoidMemo"/>); it goes to the reversal memo and the audit log.</summary>
public sealed record VoidBillCommand(Guid Id, string? Reason) : ICommand<BillDto>, IRetryOnConcurrency, IRequirePermission
{
    public string RequiredPermission => CorePermissions.BillsVoid;
}
