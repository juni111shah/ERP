using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Applies what is still unallocated on a posted payment to open bills of the vendor.</summary>
public sealed record AllocatePaymentCommand(Guid Id, List<BillAllocationInput> Allocations) : ICommand<PaymentDto>, IRetryOnConcurrency, IRequirePermission
{
    public string RequiredPermission => CorePermissions.PaymentsWrite;
}
