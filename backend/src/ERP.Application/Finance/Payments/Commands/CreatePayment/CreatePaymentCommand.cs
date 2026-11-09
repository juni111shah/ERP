using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Pays a vendor: Dr payables, Cr bank / cash (or "cheques issued" for a cheque), settling bills at the same time.</summary>
public sealed record CreatePaymentCommand(PaymentInput Input) : ICommand<PaymentDto>, IRetryOnConcurrency, IRequirePermission
{
    public string RequiredPermission => CorePermissions.PaymentsWrite;
}
