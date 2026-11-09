using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>A page of vendor payments. Receipts and payments share one table; each list only shows its own direction. Search, filter, sort and paging stay generic.</summary>
public sealed record ListPaymentsQuery(PagedQuery Paging) : IQuery<PagedResult<PaymentSummaryDto>>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.PaymentsRead;
}
