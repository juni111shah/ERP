using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Voids a posted payment by reversing its journal and its bill allocations. Reason is required (3-500 characters, see <see cref="VoidMemo"/>); it goes to the reversal memo and the audit log.</summary>
public sealed record VoidPaymentCommand(Guid Id, string? Reason) : ICommand<PaymentDto>, IRetryOnConcurrency, IRequirePermission
{
    public string RequiredPermission => CorePermissions.PaymentsWrite;
}
