using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Cash flow statement for a date range, built from the journal lines of bank and cash accounts and classified by what is on the other side of each entry.</summary>
public sealed record GetCashFlowStatementQuery(DateOnly From, DateOnly To, Guid? LegalEntityId) : IQuery<CashFlowStatementDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ReportsRead;
}
