using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>The balance of every bank and cash account as of a date.</summary>
public sealed record GetCashPositionQuery(DateOnly AsOf, Guid? LegalEntityId) : IQuery<CashPositionDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.BankRead;
}
