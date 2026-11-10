using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Sales.Features;

/// <summary>Sales, open orders and quotes, and how many quotes became orders.</summary>
public sealed record GetSalesKpiSummaryQuery() : IQuery<SalesKpiDto>, IRequirePermission
{
    public string RequiredPermission => P.OrdersRead;
}
