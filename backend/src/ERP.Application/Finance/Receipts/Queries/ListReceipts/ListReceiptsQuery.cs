using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>A page of customer receipts. Receipts and payments share one table; each list only shows its own direction. Search, filter, sort and paging stay generic.</summary>
public sealed record ListReceiptsQuery(PagedQuery Paging) : IQuery<PagedResult<ReceiptSummaryDto>>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ReceiptsRead;
}
