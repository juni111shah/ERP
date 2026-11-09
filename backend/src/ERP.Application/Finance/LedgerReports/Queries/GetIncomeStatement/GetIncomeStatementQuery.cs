using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Income and expenses for a period and the net profit. Year-end closing entries are left out.</summary>
public sealed record GetIncomeStatementQuery(DateOnly From, DateOnly To, Guid? LegalEntityId) : IQuery<IncomeStatementDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ReportsRead;
}
