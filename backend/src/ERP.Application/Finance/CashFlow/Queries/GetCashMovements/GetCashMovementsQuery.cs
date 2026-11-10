using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Opening balance, inflows, outflows and closing balance of each bank and cash account for a period, and the movement by source.</summary>
public sealed record GetCashMovementsQuery(DateOnly From, DateOnly To, Guid? LegalEntityId) : IQuery<CashMovementsDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ReportsRead;
}
