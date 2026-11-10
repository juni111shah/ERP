using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Posts a payment that waited for approval, once approved. A rejected or withdrawn one goes for approval again, as a bill does.</summary>
public sealed record PostPaymentCommand(Guid Id) : ICommand<PaymentDto>, IRetryOnConcurrency, IRequirePermission
{
    public string RequiredPermission => CorePermissions.PaymentsWrite;
}
