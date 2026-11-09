using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Assets, liabilities and equity on a date, with current earnings and what earlier unclosed years left behind.</summary>
public sealed record GetBalanceSheetQuery(DateOnly AsOf, Guid? LegalEntityId) : IQuery<BalanceSheetDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ReportsRead;
}
