using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Printing;

/// <summary>Get print data for a payment.</summary>
public sealed record GetPrintPaymentQuery(Guid Id) : IQuery<PrintPaymentDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.PaymentsRead;
}
