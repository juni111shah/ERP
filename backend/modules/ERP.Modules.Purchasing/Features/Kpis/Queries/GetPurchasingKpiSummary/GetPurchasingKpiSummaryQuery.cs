using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Purchasing.Features;

/// <summary>Spend, open orders, pending receipts and open requisitions.</summary>
public sealed record GetPurchasingKpiSummaryQuery() : IQuery<PurchasingKpiDto>, IRequirePermission
{
    public string RequiredPermission => P.OrdersRead;
}
