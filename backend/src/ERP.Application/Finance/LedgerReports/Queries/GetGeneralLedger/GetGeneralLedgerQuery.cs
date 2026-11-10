using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>The posted lines of one account for a period, with the opening balance and a running balance.</summary>
public sealed record GetGeneralLedgerQuery(Guid AccountId, DateOnly? From, DateOnly To, Guid? LegalEntityId) : IQuery<GeneralLedgerDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ReportsRead;
}
