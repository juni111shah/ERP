using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Removes a payment that never reached the ledger, withdrawing its approval request while that is still pending.</summary>
public sealed record DeletePaymentCommand(Guid Id) : ICommand, IRetryOnConcurrency, IRequirePermission
{
    public string RequiredPermission => CorePermissions.PaymentsWrite;
}
