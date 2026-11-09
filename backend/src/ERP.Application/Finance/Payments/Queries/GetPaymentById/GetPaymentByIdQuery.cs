using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>One vendor payment with its allocations.</summary>
public sealed record GetPaymentByIdQuery(Guid Id) : IQuery<PaymentDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.PaymentsRead;
}
