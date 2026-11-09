using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Trial balance for a period (opening balance, movement, closing balance per account). Draft journals never count.</summary>
public sealed record GetTrialBalanceQuery(DateOnly? From, DateOnly To, Guid? LegalEntityId) : IQuery<TrialBalanceDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ReportsRead;
}
