using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Cash today plus expected receipts and payments by week. Overdue items fall into the first week.</summary>
public sealed record GetCashForecastQuery(int Weeks, Guid? LegalEntityId) : IQuery<CashForecastDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ReportsRead;
}
